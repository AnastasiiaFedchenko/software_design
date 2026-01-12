# Lab 7: SMTP integration + mock server

## External service
SMTP (RFC 5321/5322). Real provider example: Gmail/Outlook SMTP.

## Integration scenario
2FA and recovery codes are sent by email from `WebApp/Controllers/AccountController.cs`
via `WebApp/Services/SmtpEmailSender.cs`.

## Mock server
Project `MockSmtpServer` runs a local SMTP server and stores incoming emails as `.eml`
files in an output directory.

### Run mock server manually
```
dotnet run --project MockSmtpServer/MockSmtpServer.csproj -- --host 127.0.0.1 --port 8025 --output TestResults/MockSmtp
```

### Switch between mock and real
- Mock: set `ASPNETCORE_ENVIRONMENT=Mock` so `WebApp/appsettings.Mock.json` is loaded.
- Real: use default `appsettings.json` or environment variables for SMTP.

## E2E tests
Mock SMTP E2E:
```
scripts/run-e2e-mock-smtp.sh
```

Real SMTP/IMAP E2E (requires real credentials):
```
scripts/run-e2e-real-smtp.sh
```

### Required env vars for real service
- SMTP_HOST
- SMTP_PORT (optional, default 587)
- SMTP_USER
- SMTP_PASSWORD
- SMTP_FROM
- SMTP_SSL (true/false)
- E2E_EMAIL_USER
- E2E_EMAIL_PASSWORD
- E2E_EMAIL_HOST (optional, default imap.gmail.com)
- E2E_EMAIL_PORT (optional, default 993)
- E2E_EMAIL_SSL (optional, default true)

## Demo run
Mock:
```
scripts/demo-mock-smtp.sh
```

Real:
```
scripts/demo-real-smtp.sh
```
