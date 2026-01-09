# Lab 4: E2E Auth (2FA, Lockout, Recovery)

## Что уже сделано
- 2FA + блокировка + восстановление + смена пароля в `WebApp`.
- BDD E2E‑тесты (LightBDD) в `E2E.Tests/AuthenticationBddTests.cs`.
- CI запускает E2E с секретами.

## Что нужно сделать у себя локально
1) Запуск приложения:
```
dotnet run --project WebApp/WebApp.csproj
```

### Минимум для реальной почты (локально, E2E.Tests)
Чтобы коды реально уходили по почте и читались через IMAP, в `E2E.Tests` должны быть заданы SMTP + IMAP параметры:
```
dotnet user-secrets set "SMTP_HOST" "smtp.gmail.com" --project E2E.Tests/E2E.Tests.csproj
dotnet user-secrets set "SMTP_PORT" "587" --project E2E.Tests/E2E.Tests.csproj
dotnet user-secrets set "SMTP_USER" "<sender@gmail.com>" --project E2E.Tests/E2E.Tests.csproj
dotnet user-secrets set "SMTP_PASSWORD" "<app-password SMTP>" --project E2E.Tests/E2E.Tests.csproj
dotnet user-secrets set "SMTP_FROM" "<sender@gmail.com>" --project E2E.Tests/E2E.Tests.csproj
dotnet user-secrets set "E2E_EMAIL_USER" "<receiver@gmail.com>" --project E2E.Tests/E2E.Tests.csproj
dotnet user-secrets set "E2E_EMAIL_PASSWORD" "<app-password IMAP>" --project E2E.Tests/E2E.Tests.csproj
dotnet user-secrets set "E2E_EMAIL_HOST" "imap.gmail.com" --project E2E.Tests/E2E.Tests.csproj
dotnet user-secrets set "E2E_EMAIL_PORT" "993" --project E2E.Tests/E2E.Tests.csproj
dotnet user-secrets set "E2E_EMAIL_SSL" "true" --project E2E.Tests/E2E.Tests.csproj
```

И отключи показ кодов на странице:
```
dotnet user-secrets set "AuthSettings:ShowTwoFactorCode" "false" --project E2E.Tests/E2E.Tests.csproj
dotnet user-secrets set "AuthSettings:ShowRecoveryCode" "false" --project E2E.Tests/E2E.Tests.csproj
```

2) Для локального запуска E2E через user-secrets:
```
dotnet user-secrets init --project E2E.Tests/E2E.Tests.csproj
dotnet user-secrets set "E2E_USER_PASSWORD" "<password>" --project E2E.Tests/E2E.Tests.csproj
dotnet user-secrets set "E2E_NEW_PASSWORD" "<new-password>" --project E2E.Tests/E2E.Tests.csproj
dotnet user-secrets set "E2E_EMAIL_USER" "<admin-inbox>" --project E2E.Tests/E2E.Tests.csproj
dotnet user-secrets set "E2E_EMAIL_PASSWORD" "<imap-app-password>" --project E2E.Tests/E2E.Tests.csproj
dotnet user-secrets set "E2E_EMAIL_HOST" "imap.gmail.com" --project E2E.Tests/E2E.Tests.csproj
dotnet user-secrets set "E2E_EMAIL_PORT" "993" --project E2E.Tests/E2E.Tests.csproj
dotnet user-secrets set "E2E_EMAIL_SSL" "true" --project E2E.Tests/E2E.Tests.csproj
```

2) Проверка через браузер:
- `/Account/Login` → ввод логина/пароля
- `/Account/TwoFactor` → ввод 2FA‑кода
- `/Account/Recover` → восстановление после блокировки
- `/Account/ChangePassword` → смена пароля

3) Если нужны реальные письма через SMTP:
Настрой через `dotnet user-secrets` (пример для Gmail):
```
dotnet user-secrets init --project WebApp/WebApp.csproj
dotnet user-secrets set "EmailSettings:SmtpHost" "smtp.gmail.com" --project WebApp/WebApp.csproj
dotnet user-secrets set "EmailSettings:SmtpPort" "587" --project WebApp/WebApp.csproj
dotnet user-secrets set "EmailSettings:SmtpUser" "<smtp-user>" --project WebApp/WebApp.csproj
dotnet user-secrets set "EmailSettings:SmtpPassword" "<app-password>" --project WebApp/WebApp.csproj
dotnet user-secrets set "EmailSettings:FromEmail" "<smtp-user>" --project WebApp/WebApp.csproj
dotnet user-secrets set "EmailSettings:AdminEmail" "<admin-inbox>" --project WebApp/WebApp.csproj
```

Если SMTP не настроен, коды показываются на странице (Development).

## Что нужно сделать в CI/CD
1) Добавить секреты:
- `E2E_USER_PASSWORD`
- `E2E_NEW_PASSWORD`
- `SMTP_HOST`
- `SMTP_PORT`
- `SMTP_USER`
- `SMTP_PASSWORD`
- `SMTP_FROM`
- `E2E_EMAIL_USER`
- `E2E_EMAIL_PASSWORD`
- `E2E_EMAIL_HOST` (например `imap.gmail.com`)
- `E2E_EMAIL_PORT` (например `993`)
- `E2E_EMAIL_SSL` (`true`/`false`)

## Как добавить GitHub Secrets
1) Открой репозиторий на GitHub.
2) Перейди в **Settings → Secrets and variables → Actions**.
3) Нажми **New repository secret**.
4) Добавь секреты:
   - `E2E_USER_PASSWORD`
   - `E2E_NEW_PASSWORD`
   - `SMTP_HOST`
   - `SMTP_PORT`
   - `SMTP_USER`
   - `SMTP_PASSWORD`
   - `SMTP_FROM`
   - `E2E_EMAIL_USER`
   - `E2E_EMAIL_PASSWORD`
   - `E2E_EMAIL_HOST`
   - `E2E_EMAIL_PORT`
   - `E2E_EMAIL_SSL`
5) Сохрани.

2) В workflow для `auth-e2e-tests` включено:
- `AuthSettings__ShowTwoFactorCode=false`
- `AuthSettings__ShowRecoveryCode=false`

3) Auth BDD E2E‑тесты получают коды из писем (IMAP). Если не заданы IMAP‑секреты, тесты упадут.

## Какие сценарии проверяются
- 2FA вход
- ограничение попыток
- восстановление после блокировки
- плановая смена пароля
