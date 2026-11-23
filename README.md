# waste-disposal-fee-liability

FSS (Fee Support Service) billing and organisation details API, ported from the Azure-based
`epr-calculator-fss-api` onto DEFRA's CDP (Core Delivery Platform, AWS-based) C# ASP.NET backend
template.

* [Known gaps from the Azure migration](#known-gaps-from-the-azure-migration)
* [Docker Compose](#docker-compose)
* [Testing](#testing)
* [Running](#running)
* [Dependabot](#dependabot)

### Known gaps from the Azure migration

A few Azure-specific pieces had no AWS/CDP equivalent available at the time of porting, and are
deliberately stubbed rather than silently dropped:

- **Calculator API authentication** (`Billing/Clients/EprCalculatorApiClient.cs`) - the original
  attached an Entra ID bearer token acquired via Azure Managed Identity. That mechanism doesn't
  exist on AWS, and no replacement (IAM-based service auth, mTLS, etc) has been decided - calls to
  calculator-api currently carry no authentication at all. Revisit once calculator-api's own CDP
  migration settles on an inter-service auth story.

### Docker Compose

A Docker Compose template is in [compose.yml](compose.yml).

A local environment with:

- floci (a Localstack alternative) emulating AWS services (S3, SQS) for local development.
- This service.

```bash
docker compose up --build -d
```

A more extensive setup is available in [github.com/DEFRA/cdp-local-environment](https://github.com/DEFRA/cdp-local-environment)

### Testing

Tests run a full `WebApplication` via `WebApplicationFactory<Program>`, with external
dependencies (`IEprCalculatorApiClient`, `IEprCommonDataApiClient`)
substituted using NSubstitute - there's no external datastore to stand up for tests.

```bash
dotnet test
```

### Running

```bash
dotnet run --project WasteDisposalFeeLiability --launch-profile WasteDisposalFeeLiability
```

### SonarCloud

Example SonarCloud configuration are available in the GitHub Action workflows.

### Dependabot

[.github/dependabot.yml](.github/dependabot.yml) checks NuGet packages and GitHub Actions weekly.
Minor and patch updates are grouped into a single PR; major updates get their own PR, except
`Microsoft.AspNetCore.*` majors, which are ignored because they're upgraded alongside the .NET target
framework.


### About the licence

The Open Government Licence (OGL) was developed by the Controller of Her Majesty's Stationery Office (HMSO) to enable
information providers in the public sector to license the use and re-use of their information under a common open
licence.

It is designed to encourage use and re-use of information freely and flexibly, with only a few conditions.
