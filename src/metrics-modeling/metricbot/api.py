"""POST predictions to the API — replaces the manual Postman step.

Stdlib urllib on purpose: the venv has no HTTP client and this is one
JSON POST. The endpoint is the existing admin ingestion route,
authenticated with the admin token header (AdminApiToken filter).
"""

from __future__ import annotations

import json
import logging
import urllib.error
import urllib.parse
import urllib.request

from .config import Config

logger = logging.getLogger("metricbot.api")

# urlopen honours file:/ftp: too — a misconfigured base URL would then
# silently change the destination instead of failing. Restrict it.
ALLOWED_SCHEMES = ("https", "http")


class PublishError(RuntimeError):
    pass


def post_predictions(config: Config, dtos: list[dict], timeout_seconds: int = 60) -> str:
    url = f"{config.api_base_url}/admin/ai-predictions/{config.metricbot_user_id}"

    parsed = urllib.parse.urlparse(url)
    if parsed.scheme not in ALLOWED_SCHEMES:
        raise PublishError(
            f"METRICBOT_API_BASE_URL must use https (or http for in-cluster/"
            f"local targets); got scheme '{parsed.scheme}'.")
    if parsed.scheme == "http" and parsed.hostname not in ("localhost", "127.0.0.1", "::1"):
        # In-cluster traffic is http by design (same as every other
        # service-to-service call); flag it so a mistyped public URL that
        # would ship the admin token in cleartext is visible in the logs.
        logger.warning(
            "Posting predictions over plaintext http to %s — expected for "
            "in-cluster service DNS, wrong for any public endpoint.",
            parsed.hostname)

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
