#!/usr/bin/env node
/**
 * @ff-occam/eval-harness — Evaluation harness for web extraction MCP servers
 * 
 * "Lighthouse for Web Extraction" — standardized evaluation of any MCP server
 * implementing the FF-Occam tool contract.
 */

import { program } from 'commander';
import { readFileSync, writeFileSync, existsSync, mkdirSync } from 'fs';
import { resolve, dirname } from 'path';
import { fileURLToPath } from 'url';
import yaml from 'js-yaml';
import chalk from 'chalk';
import ora from 'ora';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
import { WebSocketClientTransport } from '@modelcontextprotocol/sdk/client/websocket.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const ROOT = resolve(__dirname, '..', '..', '..');

// ============================================
// Types
// ============================================

interface CorpusEntry {
  url: string;
  expectedBackend?: 'http' | 'browser' | 'http_then_browser';
  expectedOutcome?: 'ok' | 'failure';
  expectedFailureCode?: string;
  minMarkdownLength?: number;
  focusQuery?: string;
  tags?: string[];
}

interface Corpus {
  name: string;
  description: string;
  entries: CorpusEntry[];
}

interface EvalConfig {
  mcpServer: {
    command: string;
    args: string[];
    env?: Record<string, string>;
    transport?: 'stdio' | 'websocket';
    wsUrl?: string;
  };
  corpora: string[];
  output: {
    dir: string;
    format: 'json' | 'html' | 'both';
  };
  thresholds: {
    minAccuracy: number;
    maxAvgLatencyMs: number;
    minFocusMatchHonesty: number;
  };
}

interface ToolResult {
  ok: boolean;
  url: string;
  markdown?: string;
  failure?: { code: string; message: string };
  meta?: any;
}

interface EvalCaseResult {
  entry: CorpusEntry;
  tool: string;
  args: any;
  result: ToolResult;
  latencyMs: number;
  passed: boolean;
  reasons: string[];
}

interface EvalReport {
  timestamp: string;
  config: EvalConfig;
  summary: {
    totalCases: number;
    passed: number;
    failed: number;
    accuracy: number;
    avgLatencyMs: number;
    focusMatchHonesty: number;
  };
  results: EvalCaseResult[];
  byCorpus: Record<string, { passed: number; failed: number; accuracy: number }>;
}

// ============================================
// Corpus loading
// ============================================

function loadCorpus(name: string): Corpus {
  const paths = [
    resolve(ROOT, 'corpora', `${name}.jsonl`),
    resolve(ROOT, 'corpora', 'eval-harness', `${name}.jsonl`),
    resolve(ROOT, 'corpora', 'eval-harness', `${name}.json`),
    resolve(ROOT, 'corpora', `${name}.json`)
  ];
  
  for (const path of paths) {
    if (existsSync(path)) {
      const content = readFileSync(path, 'utf8');
      if (path.endsWith('.jsonl')) {
        return {
          name,
          description: `Loaded from ${path}`,
          entries: content.trim().split('\n').map(line => JSON.parse(line))
        };
      } else {
        return JSON.parse(content);
      }
    }
  }
  
  throw new Error(`Corpus not found: ${name}`);
}

// ============================================
// MCP Client wrapper
// ============================================

function toStringEnv(
  source: NodeJS.ProcessEnv | Record<string, string | undefined>
): Record<string, string> {
  return Object.fromEntries(
    Object.entries(source).filter(
      (entry): entry is [string, string] => entry[1] !== undefined
    )
  );
}

function extractToolResultText(content: unknown): string {
  if (!Array.isArray(content) || content.length === 0) {
    return '{}';
  }

  const first = content[0];
  if (typeof first !== 'object' || first === null || !('text' in first)) {
    return '{}';
  }

  const text = Reflect.get(first, 'text');
  return typeof text === 'string' && text.length > 0 ? text : '{}';
}

class McpEvalClient {
  private client: Client | null = null;
  private transport: StdioClientTransport | WebSocketClientTransport | null = null;
  
  async connect(config: EvalConfig['mcpServer']): Promise<void> {
    this.client = new Client({
      name: 'ff-occam-eval-harness',
      version: '0.1.0'
    }, {
      capabilities: {}
    });
    
    if (config.transport === 'websocket' && config.wsUrl) {
      this.transport = new WebSocketClientTransport(new URL(config.wsUrl));
    } else {
      this.transport = new StdioClientTransport({
        command: config.command,
        args: config.args,
        cwd: ROOT,
        env: toStringEnv({
          ...process.env,
          ...config.env,
          OCCAM_HOME: config.env?.OCCAM_HOME && config.env.OCCAM_HOME !== '.'
            ? config.env.OCCAM_HOME
            : ROOT
        })
      });
    }
    
    await this.client.connect(this.transport);
  }
  
  async callTool(name: string, args: any): Promise<ToolResult> {
    if (!this.client) throw new Error('Not connected');
    
    const result = await this.client.callTool({ name, arguments: args });
    
    // Parse JSON result from MCP response
    const text = extractToolResultText(result.content);
    try {
      return JSON.parse(text);
    } catch {
      return { ok: false, url: '', failure: { code: 'parse_error', message: 'Failed to parse tool result' } };
    }
  }
  
  async listTools(): Promise<string[]> {
    if (!this.client) throw new Error('Not connected');
    const tools = await this.client.listTools();
    return tools.tools.map(t => t.name);
  }
  
  async disconnect(): Promise<void> {
    if (this.transport) {
      await this.transport.close();
    }
  }
}

// ============================================
// Evaluation logic
// ============================================

async function runEvalCase(
  client: McpEvalClient,
  entry: CorpusEntry,
  tool: string,
  args: any
): Promise<EvalCaseResult> {
  const start = Date.now();
  const result = await client.callTool(tool, args);
  const latencyMs = Date.now() - start;
  
  const reasons: string[] = [];
  let passed = true;
  
  // Check ok/failure expectation
  if (entry.expectedOutcome === 'ok' && !result.ok) {
    passed = false;
    reasons.push(`Expected ok=true, got ok=false (${result.failure?.code})`);
  } else if (entry.expectedOutcome === 'failure' && result.ok) {
    passed = false;
    reasons.push(`Expected ok=false, got ok=true`);
  }
  
  // Check failure code
  if (entry.expectedFailureCode && result.failure?.code !== entry.expectedFailureCode) {
    passed = false;
    reasons.push(`Expected failure code ${entry.expectedFailureCode}, got ${result.failure?.code}`);
  }
  
  // Check markdown length
  if (entry.minMarkdownLength && result.markdown && result.markdown.length < entry.minMarkdownLength) {
    passed = false;
    reasons.push(`Markdown too short: ${result.markdown.length} < ${entry.minMarkdownLength}`);
  }
  
  // Check focus match honesty (if focusQuery provided)
  let focusMatchHonesty = 1.0;
  if (entry.focusQuery && result.markdown) {
    const queryLower = entry.focusQuery.toLowerCase();
    const markdownLower = result.markdown.toLowerCase();
    const hasFocus = markdownLower.includes(queryLower);
    // This is a simplified check - real implementation would be more sophisticated
    focusMatchHonesty = hasFocus ? 1.0 : 0.5;
  }
  
  return {
    entry,
    tool,
    args,
    result,
    latencyMs,
    passed,
    reasons
  };
}

async function runCorpus(
  client: McpEvalClient,
  corpus: Corpus,
  config: EvalConfig
): Promise<EvalCaseResult[]> {
  const results: EvalCaseResult[] = [];
  
  for (const entry of corpus.entries) {
    if (typeof entry.url !== 'string' || entry.url.length === 0) {
      // Gate-marker rows (e.g. PB4a lessons export) are not MCP URL cases.
      console.log(`  ${chalk.yellow('↷')} skip non-URL corpus row`);
      continue;
    }

    // Determine which tool to call based on entry or default to transcode
    const tool = entry.tags?.includes('probe') ? 'occam_probe' : 
                 entry.tags?.includes('digest') ? 'occam_digest' :
                 entry.tags?.includes('map') ? 'occam_map' :
                 'occam_transcode';
    
    const args: any = { url: entry.url };
    if (entry.expectedBackend) args.backend_policy = entry.expectedBackend;
    if (entry.focusQuery) args.focus_query = entry.focusQuery;
    
    const caseResult = await runEvalCase(client, entry, tool, args);
    results.push(caseResult);
    
    const status = caseResult.passed ? chalk.green('✓') : chalk.red('✗');
    console.log(`  ${status} ${tool}(${entry.url.substring(0, 60)}...) ${caseResult.latencyMs}ms`);
    if (!caseResult.passed) {
      caseResult.reasons.forEach(r => console.log(`    ${chalk.red('→')} ${r}`));
    }
  }
  
  return results;
}

// ============================================
// Report generation
// ============================================

function generateReport(config: EvalConfig, allResults: EvalCaseResult[]): EvalReport {
  const totalCases = allResults.length;
  const passed = allResults.filter(r => r.passed).length;
  const failed = totalCases - passed;
  const accuracy = totalCases > 0 ? passed / totalCases : 0;
  const avgLatencyMs = allResults.length > 0 
    ? allResults.reduce((a, b) => a + b.latencyMs, 0) / allResults.length 
    : 0;
  const focusMatchHonesty = allResults.length > 0
    ? allResults.reduce((a, b) => a + (b.reasons.some(r => r.includes('focus')) ? 0.5 : 1), 0) / allResults.length
    : 1.0;
  
  const byCorpus: Record<string, { passed: number; failed: number; accuracy: number }> = {};
  for (const result of allResults) {
    const corpusName = 'default'; // Could be enhanced to track corpus per entry
    if (!byCorpus[corpusName]) byCorpus[corpusName] = { passed: 0, failed: 0, accuracy: 0 };
    if (result.passed) byCorpus[corpusName].passed++;
    else byCorpus[corpusName].failed++;
    byCorpus[corpusName].accuracy = byCorpus[corpusName].passed / (byCorpus[corpusName].passed + byCorpus[corpusName].failed);
  }
  
  return {
    timestamp: new Date().toISOString(),
    config,
    summary: { totalCases, passed, failed, accuracy, avgLatencyMs, focusMatchHonesty },
    results: allResults,
    byCorpus
  };
}

function writeJsonReport(report: EvalReport, outputDir: string): void {
  if (!existsSync(outputDir)) mkdirSync(outputDir, { recursive: true });
  const path = resolve(outputDir, `eval-report-${Date.now()}.json`);
  writeFileSync(path, JSON.stringify(report, null, 2));
  console.log(chalk.cyan(`\n📊 JSON report: ${path}`));
}

function writeHtmlReport(report: EvalReport, outputDir: string): void {
  if (!existsSync(outputDir)) mkdirSync(outputDir, { recursive: true });
  const path = resolve(outputDir, `eval-report-${Date.now()}.html`);
  
  const html = `<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <title>FF-Occam Evaluation Report</title>
  <style>
    body { font-family: system-ui, sans-serif; max-width: 1200px; margin: 0 auto; padding: 20px; }
    .summary { display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; margin-bottom: 24px; }
    .card { background: #f5f5f5; padding: 16px; border-radius: 8px; }
    .card h3 { margin: 0 0 8px; font-size: 14px; color: #666; }
    .card .value { font-size: 32px; font-weight: bold; }
    .passed { color: #22c55e; } .failed { color: #ef4444; }
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: 8px 12px; text-align: left; border-bottom: 1px solid #eee; }
    th { background: #fafafa; font-weight: 600; }
    .status-pass { color: #22c55e; } .status-fail { color: #ef4444; }
    .latency { font-family: monospace; }
    pre { background: #f5f5f5; padding: 8px; border-radius: 4px; overflow: auto; max-height: 200px; }
  </style>
</head>
<body>
  <h1>FF-Occam Evaluation Report</h1>
  <p>Generated: ${report.timestamp}</p>
  
  <div class="summary">
    <div class="card"><h3>Total Cases</h3><div class="value">${report.summary.totalCases}</div></div>
    <div class="card"><h3>Passed</h3><div class="value passed">${report.summary.passed}</div></div>
    <div class="card"><h3>Failed</h3><div class="value failed">${report.summary.failed}</div></div>
    <div class="card"><h3>Accuracy</h3><div class="value">${(report.summary.accuracy * 100).toFixed(1)}%</div></div>
  </div>
  
  <div class="summary">
    <div class="card"><h3>Avg Latency</h3><div class="value">${report.summary.avgLatencyMs.toFixed(0)}ms</div></div>
    <div class="card"><h3>Focus Honesty</h3><div class="value">${(report.summary.focusMatchHonesty * 100).toFixed(1)}%</div></div>
  </div>
  
  <h2>Results</h2>
  <table>
    <thead>
      <tr><th>Tool</th><th>URL</th><th>Status</th><th>Latency</th><th>Details</th></tr>
    </thead>
    <tbody>
      ${report.results.map(r => `
        <tr>
          <td>${r.tool}</td>
          <td>${r.entry.url}</td>
          <td class="${r.passed ? 'status-pass' : 'status-fail'}">${r.passed ? 'PASS' : 'FAIL'}</td>
          <td class="latency">${r.latencyMs}ms</td>
          <td>${r.reasons.join('; ') || '—'}</td>
        </tr>
      `).join('')}
    </tbody>
  </table>
  
  <h2>By Corpus</h2>
  <table>
    <thead><tr><th>Corpus</th><th>Passed</th><th>Failed</th><th>Accuracy</th></tr></thead>
    <tbody>
      ${Object.entries(report.byCorpus).map(([name, stats]) => `
        <tr><td>${name}</td><td>${stats.passed}</td><td>${stats.failed}</td><td>${(stats.accuracy * 100).toFixed(1)}%</td></tr>
      `).join('')}
    </tbody>
  </table>
</body>
</html>`;
  
  writeFileSync(path, html);
  console.log(chalk.cyan(`📊 HTML report: ${path}`));
}

// ============================================
// Main
// ============================================

async function main() {
  program
    .name('occam-eval')
    .description('Evaluation harness for web extraction MCP servers')
    .version('0.1.0')
    .option('-c, --config <path>', 'Path to eval config YAML', 'corpora/eval-harness/config.yaml')
    .option('--corpus <name>', 'Corpus to run (l0-smoke, l4-genome, quality-audit-rotation)')
    .option('--server <command>', 'MCP server command (overrides config)')
    .option('--args <args...>', 'MCP server args')
    .option('--transport <type>', 'Transport: stdio or websocket', 'stdio')
    .option('--ws-url <url>', 'WebSocket URL for websocket transport')
    .option('-o, --output <dir>', 'Output directory', 'artifacts/eval')
    .option('--format <type>', 'Output format: json, html, both', 'both')
    .option('--threshold-accuracy <n>', 'Min accuracy threshold', '0.95')
    .option('--threshold-latency <n>', 'Max avg latency (ms)', '5000')
    .option('--threshold-focus <n>', 'Min focus match honesty', '0.8')
    .action(async (options) => {
      console.log(chalk.bold.cyan('\n🔬 FF-Occam Evaluation Harness\n'));
      
      // Load config
      let config: EvalConfig;
      if (existsSync(options.config)) {
        const content = readFileSync(options.config, 'utf8');
        config = yaml.load(content) as EvalConfig;
      } else {
        // Default: local host launcher (never public npm — CI must test this commit).
        const launcher = resolve(ROOT, 'scripts', 'launch-mcp-host.mjs');
        if (!existsSync(launcher)) {
          throw new Error(
            'No MCP server command configured and scripts/launch-mcp-host.mjs is missing.\n' +
              'Pass --server <command> (and optional --args), or set mcpServer in config.yaml.'
          );
        }
        config = {
          mcpServer: {
            command: process.execPath,
            args: [launcher],
            env: { OCCAM_HOME: ROOT }
          },
          corpora: options.corpus ? [options.corpus] : ['l0-smoke'],
          output: { dir: options.output, format: options.format as any },
          thresholds: {
            minAccuracy: parseFloat(options.thresholdAccuracy),
            maxAvgLatencyMs: parseInt(options.thresholdLatency),
            minFocusMatchHonesty: parseFloat(options.thresholdFocus)
          }
        };
      }
      
      // Override from CLI
      if (options.server) {
        config.mcpServer.command = options.server;
        // Avoid leaking config/default args (e.g. old @ff-occam/mcp) onto a new command.
        if (!options.args?.length) {
          config.mcpServer.args = [];
        }
      }
      if (options.args?.length) config.mcpServer.args = options.args;
      if (options.corpus) config.corpora = [options.corpus];
      if (options.transport) config.mcpServer.transport = options.transport as any;
      if (options.wsUrl) config.mcpServer.wsUrl = options.wsUrl;
      if (options.output) config.output.dir = options.output;
      if (options.format) config.output.format = options.format as any;
      if (options.thresholdAccuracy) {
        config.thresholds.minAccuracy = parseFloat(options.thresholdAccuracy);
      }
      if (options.thresholdLatency) {
        config.thresholds.maxAvgLatencyMs = parseInt(options.thresholdLatency, 10);
      }
      if (options.thresholdFocus) {
        config.thresholds.minFocusMatchHonesty = parseFloat(options.thresholdFocus);
      }

      config.mcpServer.args = config.mcpServer.args ?? [];
      
      console.log(`MCP Server: ${config.mcpServer.command} ${config.mcpServer.args.join(' ')}`);
      console.log(`Corpora: ${config.corpora.join(', ')}`);
      console.log(`Output: ${config.output.dir} (${config.output.format})`);
      
      // Connect to MCP server
      const client = new McpEvalClient();
      const spinner = ora('Connecting to MCP server...').start();
      
      try {
        await client.connect(config.mcpServer);
        spinner.succeed('Connected to MCP server');
        
        // List available tools
        const tools = await client.listTools();
        console.log(`Available tools: ${tools.join(', ')}`);
        
        // Run each corpus
        const allResults: EvalCaseResult[] = [];
        
        for (const corpusName of config.corpora) {
          console.log(chalk.bold(`\n📋 Running corpus: ${corpusName}`));
          const corpus = loadCorpus(corpusName);
          console.log(`  ${corpus.entries.length} cases`);
          
          const results = await runCorpus(client, corpus, config);
          allResults.push(...results);
        }
        
        await client.disconnect();
        
        // Generate report
        const report = generateReport(config, allResults);
        
        if (config.output.format === 'json' || config.output.format === 'both') {
          writeJsonReport(report, config.output.dir);
        }
        if (config.output.format === 'html' || config.output.format === 'both') {
          writeHtmlReport(report, config.output.dir);
        }
        
        // Summary
        console.log(chalk.bold('\n📈 Summary'));
        console.log(`  Total:   ${report.summary.totalCases}`);
        console.log(`  Passed:  ${chalk.green(report.summary.passed.toString())}`);
        console.log(`  Failed:  ${chalk.red(report.summary.failed.toString())}`);
        console.log(`  Accuracy: ${report.summary.accuracy >= config.thresholds.minAccuracy ? chalk.green : chalk.red}(${report.summary.accuracy.toFixed(1)})`);
        console.log(`  Avg Latency: ${report.summary.avgLatencyMs <= config.thresholds.maxAvgLatencyMs ? chalk.green : chalk.red}(${report.summary.avgLatencyMs.toFixed(0)}ms)`);
        console.log(`  Focus Honesty: ${report.summary.focusMatchHonesty >= config.thresholds.minFocusMatchHonesty ? chalk.green : chalk.red}(${report.summary.focusMatchHonesty.toFixed(1)})`);
        
        // Exit code
        const success = report.summary.accuracy >= config.thresholds.minAccuracy &&
                       report.summary.avgLatencyMs <= config.thresholds.maxAvgLatencyMs &&
                       report.summary.focusMatchHonesty >= config.thresholds.minFocusMatchHonesty;
        
        process.exit(success ? 0 : 1);
        
      } catch (error) {
        spinner.fail('Failed to connect to MCP server');
        console.error(chalk.red('Error:'), error);
        process.exit(1);
      }
    });
  
  program.parse();
}

main().catch(console.error);