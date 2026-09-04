# Rates

Платформа валютных курсов на базе .NET 8, чистой архитектуры и CQRS.

Система состоит из следующих компонентов:

| Компонент | Назначение |
|---|---|
| `Rates.MigrationService.Host` | Одноразовый процесс, применяющий миграции EF Core к обеим базам данных — `Identity` и `Finance`. |
| `Rates.UserService.Api` | Регистрация, вход, обновление и выход с использованием JWT; управление избранными валютами пользователя. |
| `Rates.FinanceService.Api` | Возвращает избранные валюты аутентифицированного пользователя из таблицы `currency`, заполняемой worker-процессом. |
| `Rates.RatesWorker.Host` | Периодически получает ежедневные курсы из XML-ленты Центрального банка России и добавляет или обновляет их в таблице `currency`. |
| `Rates.ApiGateway.Api` | Шлюз на базе YARP, перенаправляющий запросы `/api/v1/auth/**`, `/api/v1/users/**` и `/api/v1/finance/**` в соответствующие сервисы и проверяющий JWT на границе системы. |

## Структура проекта

```
src/
├── BuildingBlocks/           # Общие абстракции (Domain / Application / Infrastructure / Contracts)
└── Services/
    ├── Migration/            # MigrationService.Application / Infrastructure / Host
    ├── User/                 # UserService.Domain / Application / Infrastructure / Api
    ├── Finance/              # FinanceService.Domain / Application / Infrastructure / Api
    ├── RatesWorker/          # RatesWorker.Host
    └── ApiGateway/           # ApiGateway.Api

tests/                         # Проекты xUnit (модульные и интеграционные тесты)
docker-compose.yml              # Локальный Postgres и сервисы
```

## Предварительные требования

* .NET 8 SDK (8.0.4xx или более поздняя версия).
* Docker / Docker Compose (рекомендуется для локального запуска Postgres).
* Ключ подписи для JWT (переменная окружения `JWT_SIGNING_KEY` длиной не менее 32 символов).

## Сборка

```bash
dotnet restore Rates.sln
dotnet build Rates.sln -c Release
```

## Локальный запуск с Docker

```bash
export JWT_SIGNING_KEY="dev-only-do-not-use-in-production-1234567890"
docker compose up --build
```

* Шлюз: <http://localhost:8080>
* Web UI для проверки сервиса: <http://localhost:8080/> (регистрация, вход, избранные валюты)
* User API (напрямую): <http://localhost:5101/swagger>
* Finance API (напрямую): <http://localhost:5102/swagger>
* Postgres: `localhost:5432` (`rates` / `rates`)

Сервис `migration` запускается один раз при старте и завершается с кодом 0 в случае успеха. Контейнер
`rates-worker` получает данные ЦБ при запуске, а затем повторяет загрузку через каждый интервал,
заданный параметром `CbrRates:UpdateInterval`.

## Web UI

Минимальный SPA, встроенный в `Rates.ApiGateway.Api`, для ручной проверки всех базовых сценариев:
регистрация, вход, выход, добавление и удаление избранных валют. Статические файлы
(`index.html`, `app.js`, `styles.css`) лежат в `src/Services/ApiGateway/Rates.ApiGateway.Api/wwwroot/`
и отдаются самим шлюзом — отдельный фронтенд-сервер и CORS-настройки не нужны, так как UI и API
работают с одного origin.

### Возможности

- Регистрация и вход (`POST /api/v1/auth/register`, `POST /api/v1/auth/login`).
- Выход с отзывом refresh-токена на сервере (`POST /api/v1/auth/logout`), затем клиентский сброс.
- Просмотр профиля (`GET /api/v1/users/me`).
- Список избранных валют (`GET /api/v1/users/me/favorites`).
- Добавление (`PUT /api/v1/users/me/favorites/{code}`) и удаление (`DELETE /api/v1/users/me/favorites/{code}`).
- Выпадающий список доступных валют подгружается из `GET /api/v1/finance/currencies` — это
  полный каталог из таблицы `currency`, который должен быть заполнен `rates-worker`. До первой
  синхронизации список будет пуст.
- Курсы только по избранным валютам пользователя отдаются отдельным эндпоинтом
  `GET /api/v1/finance/me/favorites`. В UI он сейчас не используется.
- При ответе `401` access-токен считается недействительным — клиент чистит `localStorage`
  и возвращается на экран логина.

### Запуск UI

UI работает там же, где и шлюз, поэтому отдельной команды запуска нет. Достаточно поднять весь
стек (см. раздел «Локальный запуск с Docker» выше) и открыть в браузере корневой URL шлюза.

```bash
export JWT_SIGNING_KEY="dev-only-do-not-use-in-production-1234567890"
docker compose up --build
```

После того как контейнеры `migration` и `rates-worker` отработают успешно (`docker compose ps`
покажет их как `exited (0)` и `running` соответственно), UI готов к использованию.

1. Откройте в браузере <http://localhost:8080/>.
2. На экране входа заполните поля «Имя пользователя» и «Пароль» (минимум 6 символов).
3. Нажмите «Зарегистрироваться» — учётная запись создаётся, токены сохраняются в браузере.
   Для уже существующего пользователя используйте «Войти».
4. На экране профиля:
   - В выпадающем списке выберите валюту и нажмите «Добавить».
   - Удаление — кнопкой «Удалить» у соответствующего элемента.
5. «Выйти» отзывает refresh-токен на сервере и очищает локальные токены.

### Локальный запуск без docker

Если Postgres и сервисы поднимаются локально без Docker (например, через установленный на хосте
Postgres), запускайте проекты по очереди в отдельных терминалах.

Сначала примените миграции — `MigrationService` завершится сам после успешного применения:

```bash
export ASPNETCORE_ENVIRONMENT=Development
export ConnectionStrings__IdentityDb="Host=localhost;Database=rates_identity;Username=rates;Password=rates"
export ConnectionStrings__FinanceDb="Host=localhost;Database=rates_finance;Username=rates;Password=rates"
export Jwt__SigningKey="dev-only-do-not-use-in-production-1234567890"

dotnet run --project src/Services/Migration/Rates.MigrationService.Host
```

Затем в отдельных терминалах поднимите остальные сервисы (все используют те же переменные окружения):

```bash
dotnet run --project src/Services/User/Rates.UserService.Api
```

```bash
dotnet run --project src/Services/Finance/Rates.FinanceService.Api
```

```bash
dotnet run --project src/Services/RatesWorker/Rates.RatesWorker.Host
```

```bash
dotnet run --project src/Services/ApiGateway/Rates.ApiGateway.Api
```

Точки входа после старта:

* Шлюз (UI и публичные маршруты): <http://localhost:8080/>
* UserService (напрямую): <http://localhost:5101/swagger>
* FinanceService (напрямую): <http://localhost:5102/swagger>
* Postgres: `localhost:5432` (`rates` / `rates`)

Если `ASPNETCORE_URLS` не задан, каждый сервис использует порт из собственного
`Properties/launchSettings.json`.

## Публичные конечные точки (через шлюз)

Шлюз является единой публичной точкой входа. Прямые порты сервисов предназначены только
для локальной разработки; в production UserService и FinanceService должны находиться
в приватной сети.

| Метод | Путь | Описание |
|---|---|---|
| POST | `/api/v1/auth/register` | Создать пользователя. |
| POST | `/api/v1/auth/login` | Выдать токены доступа и обновления. |
| POST | `/api/v1/auth/refresh` | Выполнить ротацию токенов. |
| POST | `/api/v1/auth/logout` | Отозвать переданный токен обновления (требуется JWT). |
| GET  | `/api/v1/users/me` | Текущий пользователь. |
| GET  | `/api/v1/users/me/favorites` | Получить список кодов избранных валют. |
| PUT  | `/api/v1/users/me/favorites/{code}` | Добавить валюту в избранное. |
| DELETE | `/api/v1/users/me/favorites/{code}` | Удалить валюту из избранного. |
| GET | `/api/v1/finance/currencies` | Полный каталог валют с последним известным курсом по каждой. |
| GET | `/api/v1/finance/me/favorites` | Курсы только по избранным валютам текущего пользователя. |
| GET | `/health` | Локальная liveness-проверка шлюза. |
| GET | `/health/ready` | Readiness-проверка UserService и FinanceService. |

Маршрут `/internal/**` намеренно отсутствует в конфигурации шлюза. Внутренняя конечная
точка UserService для favorites доступна только в приватной сети и требует заголовок
`X-Internal-Service-Token`.

### Политики шлюза

- Auth-маршруты используют fixed-window лимит по IP: по умолчанию 10 запросов за 60 секунд.
- Защищённые маршруты пользователя и финансов используют лимит по JWT `sub` с fallback на IP:
  120 запросов за 60 секунд.
- Лимиты настраиваются параметрами `RateLimiting:AuthPermitLimit`, `RateLimiting:AuthWindowSeconds`,
  `RateLimiting:ApiPermitLimit` и `RateLimiting:ApiWindowSeconds`.
- При превышении лимита возвращаются `429`, `Retry-After` и `application/problem+json`.
- Ошибки аутентификации возвращают `401`, неизвестные маршруты — `404`, недоступные зависимости — `503`.
- Каждый ответ содержит `X-Correlation-Id`; шлюз проверяет и передаёт его downstream-сервису.
- `X-Correlation-Id` доступен браузерному клиенту через CORS `Expose-Headers`.

В Development Swagger доступен по адресу <http://localhost:8080/swagger> и содержит только
публичные маршруты шлюза. Внутренние endpoints и container hostnames в документацию не попадают.

## Соглашения

* Все проекты предназначены для `net8.0`; включены nullable reference types, предупреждения считаются ошибками.
* Централизованное управление пакетами выполняется через `Directory.Packages.props`.
* Проекты предметной области не ссылаются на EF Core, ASP.NET Core, JWT или HTTP.
* Все команды возвращают `Result<T>`, поэтому обработчики остаются синхронными.
* Ошибки содержат значение `Error`, которое API-слой преобразует в ответы с проблемами согласно RFC 7807.
