# Lab 5 report

Generated: 2026-01-11 05:38:04

| Scenario | Tracing | Logging | Duration (s) | CPU (ms) | Peak WS (MB) | Bench (us) | Alloc (KB) | Exit |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| tests_unit_tracing_off_logging_default | false | Default | 5.50 | 4609.38 | 1034.11 |  |  | 1 |
| tests_unit_tracing_on_logging_default | true | Default | 5.87 | 8390.62 | 1055.34 |  |  | 1 |
| benchmarks_tracing_off_logging_default | false | Default | 101.93 | 23281.25 | 457.51 |  |  | 1 |
| benchmarks_tracing_on_logging_default | true | Default | 95.41 | 37937.50 | 454.44 |  |  | 1 |
| benchmarks_tracing_off_logging_extended | false | Extended | 81.90 | 21578.12 | 477.36 |  |  | 1 |
| benchmarks_tracing_on_logging_extended | true | Extended | 71.62 | 20328.12 | 446.41 |  |  | 1 |

Notes:
- CPU and memory are captured from the parent process (dotnet).
- Compare tracing on/off and logging profiles by scenario pairs.
