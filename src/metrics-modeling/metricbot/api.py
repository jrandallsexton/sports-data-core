"""POST predictions to the API — replaces the manual Postman step.

Stdlib urllib on purpose: the venv has no HTTP client and this is one
JSON POST. The endpoint is the existing admin ingestion route,
authenticated with the admin token header (AdminApiToken filter).
"""

from __future__ import annotations

import json
import urllib.error
import urllib.request

from .config import Config


class PublishError(RuntimeError):
    pass


def post_predictions(config: Config, dtos: list[dict], timeout_seconds: int = 60) -> str:
    url = f"{config.api_base_url}/admin/ai-predictions/{config.metricbot_user_id}"

    request = urllib.request.Request(
        url,
        data=json.dumps(dtos).encode("utf-8"),
        headers={
            "Content-Type": "application/json",
            "X-Admin-Token": config.admin_token,
        },
        method="POST",
    )

    try:
        with urllib.request.urlopen(request, timeout=timeout_seconds) as response:
            body = response.read().decode("utf-8", errors="replace")
            return f"{response.status}: {body[:500]}"
    except urllib.error.HTTPError as ex:
        detail = ex.read().decode("utf-8", errors="replace")[:1000]
        raise PublishError(f"API returned {ex.code} for {url}: {detail}") from ex
    except urllib.error.URLError as ex:
        raise PublishError(f"Could not reach {url}: {ex.reason}") from ex
