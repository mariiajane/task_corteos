# task_corteos — выгрузка курсов валют ЦБР в БД

Консольное приложение на **.NET 8**, которое:

- Получает курсы валют ЦБР через **SOAP** (без `Service References`).
- При старте заполняет БД курсами за **последние 30 дней**.
- В режиме `--daemon` делает ежедневную выгрузку по расписанию.

Источник: сервис ЦБР DailyInfoWebServ (`https://www.cbr.ru/DailyInfoWebServ/DailyInfo.asmx`), метод `GetCursOnDateXML` (SOAPAction `http://web.cbr.ru/GetCursOnDateXML`).

## Запуск через Docker Compose (Postgres + приложение)

```bash
docker compose up --build
```

По умолчанию:
- БД: `cbr_rates` на `localhost:5432` (логин/пароль `postgres/postgres`)
- Приложение: стартует в режиме `--daemon`, сначала заполняет последние 30 дней, затем запускается ежедневно в `02:00` (Europe/Moscow).

Остановить:

```bash
docker compose down
```

## Схема БД

- `currency` — справочник валют (CharCode, имя, код ЦБР)
- `currency_rate` — курсы валют по датам (FK на `currency`, уникальность `currency_id + date`)

Миграции: `src/CbrRatesLoader/Data/Migrations`.

