# Real AI Provider Foundation

Astil Codex supports OpenAI-compatible streaming endpoints and the native Anthropic Claude Messages API without binding the core to one vendor. OpenAI-compatible profiles can target HTTPS cloud services or loopback local-model servers. Native Anthropic profiles use `https://api.anthropic.com/v1/messages` and Anthropic-specific SSE events.

## Components

```text
AstilCodex.Core
└── IChatProviderResolver

AstilCodex.Providers
├── Configuration
│   ├── ProviderProfile
│   ├── JsonProviderSettingsStore
│   └── ProviderProfileValidator
├── OpenAICompatible
│   ├── OpenAICompatibleChatProvider
│   └── choices/delta SSE parser
├── Anthropic
│   ├── AnthropicMessagesChatProvider
│   └── content_block_delta/text_delta SSE parser
├── ProviderFactory and shared health/model checks
├── Security
│   ├── ISecretStore
│   ├── DpapiFileSecretStore
│   └── InMemorySecretStore for tests
└── ConfiguredChatProviderResolver

AstilCodex.ProviderSetup.Cli
└── local interactive configuration and connection testing
```

## Configure a provider

Run on Windows from the repository root:

```powershell
dotnet run --project src/AstilCodex.ProviderSetup.Cli --configuration Release
```

The setup utility can configure one local and one cloud profile, test `/models`, replace credentials, and remove a profile and its credential. The cloud slot can use either an OpenAI-compatible service or native Anthropic Claude.

Default examples:

```text
Local compatible:  http://127.0.0.1:11434/v1/chat/completions
Cloud compatible:  https://api.openai.com/v1/chat/completions
Anthropic Claude:  https://api.anthropic.com/v1/messages
```

These are editable examples. Enter a model ID available to the selected account. Native Anthropic setup defaults to `claude-sonnet-5`, but the setup health check lists models actually available to the API key.

Restart `AstilCodex.Core.Host` after changing provider settings. The host loads configuration at startup and retains `mock.local` as a fallback when no eligible provider profile is configured.

## Local files

Non-secret profile settings:

```text
%LOCALAPPDATA%\AstilCodex\config\providers.json
```

DPAPI-encrypted credential blobs:

```text
%LOCALAPPDATA%\AstilCodex\secrets\<sha256-id>.bin
```

The JSON file contains endpoint, model, limits, location, and an opaque secret identifier. It never contains the API key. Credential blobs are encrypted for the current Windows user with DPAPI and cannot be decrypted under another Windows account.

## Routing behavior

| Core decision | Provider selected |
|---|---|
| Local | Configured local provider, otherwise `mock.local` |
| Cloud | Configured cloud provider, otherwise `mock.local` |
| Ask | No provider call until user/provider approval is resolved |
| Unavailable | No provider call |

Examples:

- **Auto — Privacy First:** ordinary personal chat is routed locally.
- **Cloud Preferred:** eligible non-confidential prompts use the cloud profile.
- **Local Only:** never selects the cloud profile.
- **Confidential Developer request:** remains local under the baseline policy.

The provider receives conversation text only after deterministic routing has selected its location. It never receives tool authority.

## Transport security

- Remote endpoints require HTTPS.
- Plain HTTP is accepted only for `localhost` or an IP loopback address.
- Credentials embedded in endpoint URLs are rejected.
- URL fragments are rejected.
- OpenAI-compatible keys are sent only in the `Authorization: Bearer` header.
- Anthropic keys are sent only in `x-api-key`, with `anthropic-version: 2023-06-01`.
- Credentials are never included in JSON request bodies, settings files, SQLite memory, or provider error messages.
- Streaming responses have cancellation and configurable timeout support.

## Current limitations

- Provider setup is a local console utility; Unity settings UI is the next provider submilestone.
- Only one enabled local and one enabled cloud profile are supported.
- Health checks expect a `/v1/models`-style response from compatible and Anthropic profiles.
- Provider usage and cost accounting are not implemented.
- Context selection currently includes a fixed system instruction and up to 24 recent messages.
- Profile changes require restarting the core host.

## Tests

The production self-test suite uses fake in-memory HTTP handlers and does not contact or charge a real provider. It covers endpoint security, settings persistence, secret separation, OpenAI-compatible streaming, Anthropic Messages streaming and headers, model health, local/cloud selection, cancellation, and Windows DPAPI when running on Windows.
