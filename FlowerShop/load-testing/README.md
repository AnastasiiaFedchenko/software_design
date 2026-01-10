# Нагрузочное тестирование FlowerShop (ЛР 3)

Набор сценариев и скриптов для измерения производительности WebApp2 с повторяемыми условиями.

## Объект оценки
- WebApp2 (REST API) + PostgreSQL.
- Основные эндпоинты: `GET /api/products`, `GET /api/products/{id}`, `POST /api/cart/items`, `GET /api/cart`, `PATCH /api/cart/products/{id}`, `DELETE /api/cart/products/{id}`, `POST /api/auth/login`.

## Сценарии
1. `read` — легкие чтения (список товаров + товар по id).
2. `mixed` — логин + операции с корзиной (чтение/запись).
3. `stress` — поиск точки деградации, удержание максимальной нагрузки и восстановление (ramp-up -> hold -> ramp-down).

## Условия эксперимента
- Каждый прогон поднимает новый Docker-образ с тем же Dockerfile и конфигурацией.
- Каждый прогон поднимает отдельные контейнеры, затем они удаляются.
- Нагрузка генерируется k6, результаты сохраняются в `load-testing/results`.
- Утилизация ресурсов собирается через `docker stats`.

## Быстрый старт
1. Установить зависимости:
   - Docker Desktop
   - k6
   - Python 3.x (для анализа)

2. Запуск 100 прогонов:
```
load-testing\scripts\run-benchmark.cmd
```

Для MinGW / bash:
```
bash load-testing/scripts/run-benchmark.sh
```

Если запускаете `docker compose` вручную, образ по умолчанию собирается как `flowershop-api:dev`.

3. Запуск с параметрами:
```
load-testing\scripts\run-benchmark.cmd -Runs 10 -Scenarios read,mixed
```

Для MinGW / bash:
```
bash load-testing/scripts/run-benchmark.sh --runs 10 --scenarios read,mixed
```

## Результаты
Каждый прогон сохраняется в:
```
load-testing\results\run_XXXX\<scenario>\
```

Файлы:
- `k6.json` — поток метрик.
- `summary.json` — агрегаты k6 (p50, p75, p90, p95, p99).
- `docker-stats.csv` — утилизация ресурсов.

## Агрегация по 100 прогонам
Пример агрегации для сценария `read`:
```
python load-testing/tools/aggregate_k6_summaries.py "load-testing/results/run_*/read/summary.json" load-testing/results/aggregate_read
```

## Графики и распределения
Для построения:
```
python load-testing/tools/analyze_k6_json.py --input-glob "load-testing/results/run_*/read/k6.json" --out-dir load-testing/results/graphs_read
```

Скрипт генерирует:
- `time_series.csv` (средняя задержка по времени)
- `histogram.csv` (гистограмма)
- `percentiles.csv` (p50, p75, p90, p95, p99)
- PNG-графики, если установлен `matplotlib`

Можно уменьшить количество точек по времени:
```
python load-testing/tools/analyze_k6_json.py --input-glob "load-testing/results/run_*/read/k6.json" --out-dir load-testing/results/graphs_read --max-points 28
```

## Графики ресурсов (CPU/RAM)
```
python load-testing/tools/plot_docker_stats.py --input-glob "load-testing/results/run_*/read/docker-stats.csv" --out-dir load-testing/results/graphs_resources_read
python load-testing/tools/plot_docker_stats.py --input-glob "load-testing/results/run_*/mixed/docker-stats.csv" --out-dir load-testing/results/graphs_resources_mixed
python load-testing/tools/plot_docker_stats.py --input-glob "load-testing/results/run_*/stress/docker-stats.csv" --out-dir load-testing/results/graphs_resources_stress
```
PNG-графики создаются, если установлен `matplotlib`.

## Итоги по ресурсам
```
python load-testing/tools/summarize_docker_stats.py load-testing/results/run_0001/read/docker-stats.csv load-testing/results/run_0001/read/docker-stats.summary.json
```

JSON содержит `min/max/avg` по CPU/RAM/IO для каждого контейнера.

## Примечания
- Для повторяемости используется фиксированный набор данных (см. `load-testing/db/init.sql`).
- Для учетных данных используется тестовый пользователь `id=1`, `password=pass1`.
