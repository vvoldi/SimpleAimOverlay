# Feedback service

Cloudflare Worker for the desktop Feedback tab. Public endpoint:
https://simple-aim-overlay-feedback.simple-aim-overlay-feedback.workers.dev

The desktop app builds an allowlisted report and opens the review page with a base64url JSON fragment. Fragments are not sent in the initial HTTP request; the page removes the fragment from browser history. The user reviews the report, agrees to publication and submits after Turnstile verification. GitHub authentication is not required for the sender.

The Worker validates the request, verifies the Turnstile hostname/action, applies rate limits and posts to **vvoldi/SimpleAimOverlay**. Issues are created by the owner of the server token, labelled `bug` or `enhancement`, with an explicit anonymous-feedback attribution. Never accept a repository or a credential from the client.

## Deployment

- Install dependencies with `pnpm install --frozen-lockfile` (Node 22+).
- Authenticate with `pnpm exec wrangler login`.
- Set `TURNSTILE_SITE_KEY` and `TURNSTILE_HOSTNAME` in `wrangler.jsonc`.
- Store `TURNSTILE_SECRET` and `GITHUB_TOKEN` as **Worker secrets**, using the dashboard or `pnpm exec wrangler secret put NAME`.
- GitHub: use a fine-grained token restricted to this repository with **Issues: read and write**. No contents/admin permissions are needed. Rotate the secret when revoking or renewing the token.
- Run `node --test test/*.test.mjs`, then `pnpm exec wrangler deploy`.

Never put tokens in the EXE, source, command-line arguments, screenshots, logs or chat. `.dev.vars*`, `.env*`, `.wrangler/` and `node_modules/` are ignored. Production request-body logging is disabled.

## Limits and verification

Descriptions: 10–2000 characters. Request body: at most 16 KB, enforced while reading the stream. Technical fields are allowlisted; unknown fields are ignored. The application omits account names, paths, drawings and logs. The report may be submitted without technical details.

Turnstile tokens must match the configured hostname and `feedback` action. The bindings limit anonymous clients to approximately 3 attempts/minute per IP and 10/minute overall **per Cloudflare location**; these are not strict worldwide quotas. Shared IPs share the client limit. No automatic retries are made after GitHub failures/timeouts to avoid duplicate issues. The page advises checking existing issues before retrying an uncertain delivery.

Unit tests mock outbound requests. To verify the live integration, submit one explicitly marked synthetic test report through the browser, confirm its author/body on GitHub, then close the test issue. Do not disable Turnstile for a production test.
