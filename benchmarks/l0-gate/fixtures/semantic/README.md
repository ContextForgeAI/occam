# Semantic materialization fixtures

Frozen Markdown for L1a (`SemanticMaterializationUnitTests`). No network.
Expectations are pinned in `manifest.jsonl`.

Cases: instruction + neighbor, negation/exception, nested steps, table units,
code indentation, multilingual. Each case is run with no-focus, focus+fit at
an adequate budget (650), and focus+fit at a tight budget (128), plus
transcode/digest parity.
