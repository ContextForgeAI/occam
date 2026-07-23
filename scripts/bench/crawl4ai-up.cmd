@echo off
REM Start the crawl4ai benchmark sidecar (3rd arm in scripts/bench/sweep.mjs).
REM crawl4ai 0.9.0, pinned by digest for reproducibility. Binds 0.0.0.0 (token-gated)
REM via the mounted crawl4ai-config.yml so the host can reach it through the port map.
setlocal
set IMAGE=unclecode/crawl4ai@sha256:385042cba2a216c257ccb77b0135dec5228ee25bf675edbc7487eb155bd5e644
set NAME=crawl4ai-bench
set TOKEN=occam-bench-local
set PORT=11235

echo Pulling %IMAGE% ...
docker pull %IMAGE% >nul

docker rm -f %NAME% >nul 2>&1
echo Starting %NAME% on :%PORT% ...
docker run -d --name %NAME% -p %PORT%:%PORT% --shm-size=1g ^
  -e CRAWL4AI_API_TOKEN=%TOKEN% ^
  -v "%~dp0crawl4ai-config.yml:/app/config.yml:ro" ^
  %IMAGE%

echo Waiting for health (token-gated) ...
for /l %%i in (1,1,30) do (
  for /f %%c in ('curl -s -m5 -o NUL -w "%%{http_code}" -H "Authorization: Bearer %TOKEN%" http://localhost:%PORT%/health') do (
    if "%%c"=="200" ( echo crawl4ai sidecar READY on http://localhost:%PORT% & goto :done )
  )
  timeout /t 3 /nobreak >nul
)
echo TIMEOUT - check: docker logs %NAME%
exit /b 1
:done
endlocal
