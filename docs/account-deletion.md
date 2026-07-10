# Account deletion

SonicRelay supports two deletion paths, both implemented as **soft delete**: the account
is disabled rather than physically removed so the audit trail and foreign-key integrity
survive. A disabled account cannot log in or refresh, all of its devices are revoked and
all of its active sessions are ended.

## What "soft delete" does

When an account is deleted the API:

1. Sets `IsDisabled = true` on the user.
2. Enables Identity lockout with `LockoutEnd = DateTimeOffset.MaxValue`, so the stock
   `/auth/login` endpoint rejects the account (`401 Unauthorized`).
3. Rotates the security stamp (`UpdateSecurityStampAsync`), invalidating every issued
   refresh token so `/auth/refresh` fails.
4. Revokes every device owned by the user.
5. Ends every `waiting`/`active` stream session owned by the user.
6. Writes a structured audit log entry (`Account deletion: target=… requestedBy=… reason=…`).
7. Fires the account-deletion webhook (see [n8n integration](#n8n-integration)).

Hard delete (physical row removal with cascade) is intentionally **not** performed in the
MVP. If it becomes necessary, cascade Identity user, refresh tokens, devices, stream
sessions, participants and signaling events.

## Endpoints

### Self-service (Flutter / Windows apps)

```http
DELETE /api/account
Authorization: Bearer <user_access_token>
```

| Status | Meaning |
| --- | --- |
| `204 No Content` | Account disabled. |
| `401 Unauthorized` | Missing/invalid token. |

The Flutter and Windows clients call this endpoint from their "Delete account" flow after
an explicit confirmation. The user is signed out locally afterwards.

### Administrative (n8n / operators)

```http
DELETE /api/admin/users/{userId}
Authorization: Bearer <admin_access_token>
```

| Status | Meaning |
| --- | --- |
| `204 No Content` | User disabled. |
| `400 Bad Request` | Admin attempted to delete their own account through the admin endpoint. |
| `401 Unauthorized` | Missing/invalid token. |
| `403 Forbidden` | Caller is not in the `admin` role. |
| `404 Not Found` | No user with that id. |

Requires the `AdminOnly` policy (role `admin`). Seed an admin with the `Admin:Email` /
`Admin:Password` configuration keys; on startup the API ensures the `admin` role exists and
promotes that user.

## n8n integration

Set the webhook URL the API should call when an account is deleted:

```
Notifications__AccountDeletionWebhookUrl=https://<n8n-host>/webhook/account-deletion
```

When unset (tests, local dev) the API uses a no-op notifier. The webhook receives a JSON
`POST`; delivery failures are logged and never block the deletion:

```json
{
  "userId": "0c3b…",
  "email": "user@example.com",
  "reason": "SelfService | AdminAction",
  "requestOrigin": "203.0.113.10",
  "requestedAt": "2026-07-09T12:34:56.000+00:00"
}
```

The companion n8n workflow (**"SonicRelay – Account deletion email"**) is triggered by this
webhook and emails `vitorhugoalvesferreira@gmail.com` a notification for every deletion
request. To have n8n also perform the deletion administratively, it can call
`DELETE /api/admin/users/{userId}` with an admin bearer token.
