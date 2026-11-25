#!/usr/bin/env python3
"""Console helper for the Sketchfab Data API v3 (https://api.sketchfab.com/v3).

Only the Python standard library is used, so the script can live in ``Tools/``
(outside ``Assets/``) and run without touching the Unity project.

Commands
--------
search "<query>" [--max-faces N] [--limit N] [--license cc0]
    Lists free downloadable models (``downloadable=true``) with UID, name,
    author, license and polygon count (``faceCount``).  ``--max-faces`` is sent
    to the API as ``max_face_count`` and additionally enforced client side.
    ``--license cc0`` forwards ``license=cc0`` so only public-domain models are
    returned (no attribution required).

likes [--limit N] [--cc0] [--downloadable]
    Lists the models the token owner has liked (``/v3/me/likes``) with the same
    UID / faces / GLB / license / author / name columns as ``search``.

download <uid> [--out DIR] [--force]
    Resolves a temporary archive URL through ``/v3/models/{uid}/download`` and
    stores the single-file ``.glb`` archive (binary glTF) as
    ``<safe name>_<uid>.glb``.  The ``.gltf`` / ``source`` ZIP archives are
    never downloaded: if the model has no ``.glb`` flavour the script stops and
    reports which flavours are available instead.

Authentication
--------------
The API token is read from the ``SKETCHFAB_API_TOKEN`` environment variable and
sent as ``Authorization: Token <token>``.

    PowerShell:  $env:SKETCHFAB_API_TOKEN = "<token>"
    cmd.exe:     set SKETCHFAB_API_TOKEN=<token>
    bash:        export SKETCHFAB_API_TOKEN=<token>

Exit codes
----------
0   success
1   API / network / filesystem error
2   requested format is not available (no ``.glb`` for this model)

Examples
--------
    python Tools/sketchfab_fetch.py search "military bed" --max-faces 40000 --limit 10
    python Tools/sketchfab_fetch.py download <uid> --out Assets/Models/Props/Bunker/_Staging
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path
from typing import Any, Dict, List, Optional

API_BASE = "https://api.sketchfab.com/v3"
TOKEN_ENV = "SKETCHFAB_API_TOKEN"
USER_AGENT = "DroneSimulator-sketchfab-fetch/1.0 (+python-urllib)"
PAGE_SIZE = 24  # Sketchfab caps the search page size at 24
CHUNK_SIZE = 256 * 1024
DEFAULT_OUT_DIR = Path("Assets/Models/Props/Bunker/_Staging")
MAX_PAGES = 50  # hard stop for cursor pagination
CC0_LICENSE = "cc0"  # /v3/search license filter for public-domain models

EXIT_OK = 0
EXIT_ERROR = 1
EXIT_NOT_AVAILABLE = 2

GLB_MAGIC = b"glTF"


class SketchfabError(RuntimeError):
    """Raised for API, network, filesystem and validation problems."""


class FormatUnavailable(SketchfabError):
    """Raised when the model has no single-file ``.glb`` archive."""


# --------------------------------------------------------------------------- #
# HTTP helpers
# --------------------------------------------------------------------------- #
def load_token() -> str:
    """Returns the API token or raises a helpful error when it is missing."""
    token = os.environ.get(TOKEN_ENV, "").strip()
    if not token:
        raise SketchfabError(
            "Environment variable {0} is not set.\n"
            "Create an API token at https://sketchfab.com/settings/password and set it:\n"
            "  PowerShell: $env:{0} = \"<token>\"\n"
            "  cmd.exe:    set {0}=<token>\n"
            "  bash:       export {0}=<token>".format(TOKEN_ENV)
        )
    return token


def _open(url: str, token: Optional[str] = None, accept: str = "application/json",
          timeout: int = 60):
    """Opens ``url`` and maps network/HTTP failures to :class:`SketchfabError`."""
    headers = {"User-Agent": USER_AGENT, "Accept": accept}
    if token:
        headers["Authorization"] = "Token " + token
    request = urllib.request.Request(url, headers=headers)
    try:
        return urllib.request.urlopen(request, timeout=timeout)
    except urllib.error.HTTPError as exc:
        body = ""
        try:
            body = exc.read().decode("utf-8", "replace").strip()
        except Exception:  # noqa: BLE001 - body is best effort only
            pass
        detail = body[:400] if body else "(empty response body)"
        if exc.code in (401, 403):
            hint = " Check that SKETCHFAB_API_TOKEN is valid and has the 'download' scope."
        elif exc.code == 429:
            hint = " Rate limited by Sketchfab, wait a moment and retry."
        else:
            hint = ""
        raise SketchfabError(
            "HTTP {0} {1} for {2}{3}\n{4}".format(exc.code, exc.reason, url, hint, detail)
        ) from exc
    except urllib.error.URLError as exc:
        raise SketchfabError("network error for {0}: {1}".format(url, exc.reason)) from exc


def api_get_json(path: str, params: Optional[Dict[str, Any]] = None,
                 token: Optional[str] = None) -> Dict[str, Any]:
    """GET ``API_BASE + path`` and returns the decoded JSON object."""
    url = API_BASE + path
    if params:
        url += "?" + urllib.parse.urlencode(params)
    with _open(url, token=token) as response:
        raw = response.read()
    try:
        payload = json.loads(raw.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise SketchfabError("invalid JSON from {0}: {1}".format(url, exc)) from exc
    if not isinstance(payload, dict):
        raise SketchfabError("unexpected JSON payload from {0}".format(url))
    return payload


# --------------------------------------------------------------------------- #
# search
# --------------------------------------------------------------------------- #
def search_models(token: str, query: str, max_faces: Optional[int],
                  limit: int, license_filter: Optional[str] = None,
                  sort_by: Optional[str] = None,
                  tags: Optional[str] = None,
                  endpoint: str = "/search") -> List[Dict[str, Any]]:
    """Returns up to ``limit`` free downloadable models matching ``query``."""
    collected: List[Dict[str, Any]] = []
    cursor: Optional[str] = None
    for _page in range(MAX_PAGES):
        params: Dict[str, Any] = {
            "type": "models",
            "downloadable": "true",
            "count": PAGE_SIZE,
        }
        if query:
            params["q"] = query
        if tags:
            params["tags"] = tags
        if max_faces is not None:
            params["max_face_count"] = max_faces
        if license_filter:
            params["license"] = license_filter
        if sort_by:
            params["sort_by"] = sort_by
        if cursor:
            params["cursor"] = cursor

        payload = api_get_json(endpoint, params, token)
        batch = payload.get("results") or []
        if not batch:
            break

        for item in batch:
            faces = item.get("faceCount")
            # the API already filters by max_face_count, keep a local guard too
            if max_faces is not None and isinstance(faces, int) and faces > max_faces:
                continue
            if item.get("isDownloadable") is False:
                continue
            collected.append(item)
            if len(collected) >= limit:
                return collected

        cursor = (payload.get("cursors") or {}).get("next")
        if not cursor:
            break
    return collected


def _truncate(text: Any, width: int) -> str:
    value = " ".join(str(text).split())
    return value if len(value) <= width else value[: max(width - 3, 1)] + "..."


def _format_size(size: float) -> str:
    if size >= 1024 * 1024:
        return "{0:.1f} MB".format(size / 1048576.0)
    return "{0:.0f} KB".format(size / 1024.0)


def _glb_info(model: Dict[str, Any]) -> str:
    """Formats the ``.glb`` availability reported by the ``archives`` block."""
    archives = model.get("archives") or {}
    glb = archives.get("glb") or {}
    size = glb.get("size")
    if not size:
        return "no"
    return _format_size(size)


def _print_models(models: List[Dict[str, Any]]) -> None:
    """Prints the UID / faces / GLB / license / author / name table."""
    rows = [
        (
            str(model.get("uid", "")),
            format(model.get("faceCount") or 0, ","),
            _glb_info(model),
            _truncate((model.get("license") or {}).get("label") or "unknown", 30),
            _truncate((model.get("user") or {}).get("username") or "unknown", 18),
            _truncate(model.get("name") or "(unnamed)", 46),
        )
        for model in models
    ]
    headers = ("UID", "FACES", "GLB", "LICENSE", "AUTHOR", "NAME")
    right_aligned = {1, 2}
    widths = [
        max([len(headers[i])] + [len(row[i]) for row in rows])
        for i in range(len(headers))
    ]
    header_line = "  ".join(
        header.rjust(widths[i]) if i in right_aligned else header.ljust(widths[i])
        for i, header in enumerate(headers)
    )
    print(header_line)
    print("-" * len(header_line))
    for row in rows:
        print("  ".join(
            row[i].rjust(widths[i]) if i in right_aligned else row[i].ljust(widths[i])
            for i in range(len(row))
        ))
    print("")
    print("Found {0} model(s). Download one with:\n"
          "  python {1} download <uid> --out {2}".format(
              len(rows), Path(sys.argv[0]).name, DEFAULT_OUT_DIR.as_posix()))


def cmd_search(args: argparse.Namespace) -> int:
    token = load_token()
    models = search_models(token, args.query, args.max_faces, args.limit,
                           license_filter=args.license, sort_by=args.sort,
                           tags=args.tags, endpoint=args.endpoint)

    print('Sketchfab {5}: "{0}" (free, downloadable{1}{2}{3}{4})'.format(
        args.query,
        ", max {0} faces".format(format(args.max_faces, ",")) if args.max_faces else "",
        ", license {0}".format(args.license) if args.license else "",
        ", tags {0}".format(args.tags) if args.tags else "",
        ", sort {0}".format(args.sort) if args.sort else "",
        args.endpoint,
    ))
    if not models:
        print("No free downloadable models found. "
              "Try a shorter query, raise --max-faces or drop --license.")
        return EXIT_OK

    _print_models(models)
    return EXIT_OK


# --------------------------------------------------------------------------- #
# likes
# --------------------------------------------------------------------------- #
def _license_is_cc0(model: Dict[str, Any]) -> bool:
    license_info = model.get("license") or {}
    label = (license_info.get("label") or "").lower()
    slug = (license_info.get("slug") or "").lower()
    return slug == CC0_LICENSE or label.startswith("cc0") or "public domain" in label


def fetch_likes(token: str, limit: int, cc0_only: bool = False,
                downloadable_only: bool = False) -> List[Dict[str, Any]]:
    """Returns up to ``limit`` models liked by the token owner."""
    collected: List[Dict[str, Any]] = []
    cursor: Optional[str] = None
    for _page in range(MAX_PAGES):
        params: Dict[str, Any] = {"count": PAGE_SIZE}
        if cursor:
            params["cursor"] = cursor
        payload = api_get_json("/me/likes", params, token)
        batch = payload.get("results") or []
        if not batch:
            break
        for item in batch:
            if downloadable_only and item.get("isDownloadable") is False:
                continue
            if cc0_only and not _license_is_cc0(item):
                continue
            collected.append(item)
            if len(collected) >= limit:
                return collected
        cursor = (payload.get("cursors") or {}).get("next")
        if not cursor:
            break
    return collected


def cmd_likes(args: argparse.Namespace) -> int:
    token = load_token()
    models = fetch_likes(token, args.limit, cc0_only=args.cc0,
                         downloadable_only=args.downloadable)
    print("Sketchfab likes of the token owner{0}{1}:".format(
        " (CC0 only)" if args.cc0 else "",
        " (downloadable only)" if args.downloadable else ""))
    if not models:
        print("No liked models match the filter.")
        return EXIT_OK
    _print_models(models)
    return EXIT_OK


# --------------------------------------------------------------------------- #
# download
# --------------------------------------------------------------------------- #
def sanitize_filename(name: str, fallback: str = "model") -> str:
    """Turns a model title into a filesystem friendly stem."""
    stem = re.sub(r"[^\w\-. ]+", "_", name or "", flags=re.UNICODE)
    stem = re.sub(r"\s+", "_", stem).strip("._-")
    stem = re.sub(r"_{2,}", "_", stem)
    return stem[:80].strip("._-") or fallback


def _progress(done: int, total: int) -> None:
    if total <= 0:
        sys.stdout.write("\r  downloaded {0}".format(_format_size(done)))
    else:
        percent = min(int(done * 100 / total), 100)
        sys.stdout.write("\r  downloading {0:3d}% ({1} / {2})".format(
            percent, _format_size(done), _format_size(total)))
    sys.stdout.flush()


def _describe_available(links: Dict[str, Any]) -> str:
    names = sorted(
        key for key, value in links.items()
        if isinstance(value, dict) and value.get("url")
    )
    return ", ".join(names) if names else "none"


def download_glb(token: str, uid: str, out_dir: Path, force: bool = False) -> Path:
    """Downloads the single-file ``.glb`` archive and returns its path."""
    detail = api_get_json("/models/{0}".format(uid), None, token)
    name = detail.get("name") or uid
    license_label = (detail.get("license") or {}).get("label") or "unknown"
    author = (detail.get("user") or {}).get("username") or "unknown"

    print("Model: {0}".format(name))
    print("Author: {0} | License: {1} | Faces: {2}".format(
        author, license_label, format(detail.get("faceCount") or 0, ",")))

    if detail.get("isDownloadable") is False:
        raise FormatUnavailable(
            "Model {0} is not downloadable (paid model or downloads disabled).".format(uid))

    links = api_get_json("/models/{0}/download".format(uid), None, token)
    glb = links.get("glb") or {}
    glb_url = glb.get("url")
    if not glb_url:
        raise FormatUnavailable(
            "Model {0} has no single-file .glb archive; available flavours: {1}.\n"
            "Refusing to download the .gltf/.zip archive - pick another model or "
            "convert it manually.".format(uid, _describe_available(links)))

    out_dir = Path(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    target = out_dir / "{0}_{1}.glb".format(sanitize_filename(name), uid)

    if target.exists() and not force:
        print("Already present (use --force to re-download): {0}".format(target))
        return target

    expected = glb.get("size") or 0
    print("Download: {0}".format(target.resolve()))
    tmp_path = target.with_name(target.name + ".part")
    done = 0
    try:
        with _open(glb_url, token=None, accept="application/octet-stream",
                   timeout=300) as response, open(tmp_path, "wb") as handle:
            total = int(response.headers.get("Content-Length") or expected or 0)
            while True:
                chunk = response.read(CHUNK_SIZE)
                if not chunk:
                    break
                if done == 0 and not chunk.startswith(GLB_MAGIC):
                    raise FormatUnavailable(
                        "Server did not return a binary glTF file "
                        "(missing 'glTF' magic header).")
                handle.write(chunk)
                done += len(chunk)
                _progress(done, total)
            if total and done != total:
                raise SketchfabError(
                    "incomplete download: {0} of {1} bytes".format(done, total))
        sys.stdout.write("\n")
        sys.stdout.flush()
        os.replace(tmp_path, target)
    except BaseException:
        sys.stdout.write("\n")
        sys.stdout.flush()
        if tmp_path.exists():
            try:
                tmp_path.unlink()
            except OSError:
                pass
        raise

    print("Saved {0} to {1}".format(_format_size(done), target))
    return target


def cmd_info(args: argparse.Namespace) -> int:
    """Prints the metadata used to validate a candidate model."""
    token = load_token()
    uid = args.uid.strip().lower()
    detail = api_get_json("/models/{0}".format(uid), None, token)
    license_info = detail.get("license") or {}
    archives = detail.get("archives") or {}
    glb = archives.get("glb") or {}
    tags = [tag.get("name") for tag in (detail.get("tags") or []) if tag.get("name")]
    print("UID:          {0}".format(uid))
    print("Name:         {0}".format(detail.get("name")))
    print("Author:       {0}".format((detail.get("user") or {}).get("username")))
    print("License:      {0} (slug: {1})".format(
        license_info.get("label"), license_info.get("slug")))
    print("Faces:        {0}".format(format(detail.get("faceCount") or 0, ",")))
    print("Vertices:     {0}".format(format(detail.get("vertexCount") or 0, ",")))
    print("Downloadable: {0}".format(detail.get("isDownloadable")))
    print("GLB size:     {0}".format(_format_size(glb["size"]) if glb.get("size") else "no"))
    print("Tags:         {0}".format(", ".join(tags[:20])))
    description = " ".join((detail.get("description") or "").split())
    if description:
        print("Description:  {0}".format(_truncate(description, 400)))
    return EXIT_OK


def cmd_thumb(args: argparse.Namespace) -> int:
    """Saves the largest available thumbnail of a model (preview only)."""
    token = load_token()
    uid = args.uid.strip().lower()
    detail = api_get_json("/models/{0}".format(uid), None, token)
    images = ((detail.get("thumbnails") or {}).get("images") or [])
    if not images:
        raise SketchfabError("model {0} has no thumbnails".format(uid))
    best = max(images, key=lambda item: (item.get("width") or 0) * (item.get("height") or 0))
    url = best.get("url")
    out_path = Path(args.out)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    with _open(url, token=None, accept="image/*") as response, open(out_path, "wb") as handle:
        handle.write(response.read())
    print("Model: {0}".format(detail.get("name")))
    print("License: {0}".format((detail.get("license") or {}).get("label")))
    print("Thumbnail ({0}x{1}) saved to {2}".format(
        best.get("width"), best.get("height"), out_path.as_posix()))
    return EXIT_OK


def cmd_download(args: argparse.Namespace) -> int:
    token = load_token()
    uid = args.uid.strip().lower()
    if not re.fullmatch(r"[0-9a-f]{32}", uid):
        raise SketchfabError(
            "Invalid model uid '{0}' - expected 32 hexadecimal characters "
            "(copy it from the search output).".format(args.uid))
    target = download_glb(token, uid, Path(args.out), force=args.force)
    try:
        shown = target.resolve().relative_to(Path.cwd())
    except ValueError:
        shown = target.resolve()
    print("Ready in Unity: {0}".format(shown.as_posix()))
    return EXIT_OK


# --------------------------------------------------------------------------- #
# CLI
# --------------------------------------------------------------------------- #
def _positive_int(value: str) -> int:
    try:
        parsed = int(value, 10)
    except (TypeError, ValueError):
        raise argparse.ArgumentTypeError(
            "expected an integer, got {0!r}".format(value))
    if parsed <= 0:
        raise argparse.ArgumentTypeError("must be greater than zero")
    return parsed


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="sketchfab_fetch.py",
        description="Search free downloadable Sketchfab models and fetch .glb files "
                    "using the Sketchfab Data API v3.",
        epilog="The API token is read from the {0} environment variable.".format(TOKEN_ENV),
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    search = subparsers.add_parser(
        "search", help="list free downloadable models for a query")
    search.add_argument("query", nargs="?", default="",
                        help='search text, e.g. "military bed" (optional with --tags)')
    search.add_argument("--tags", default=None, metavar="TAGS",
                        help="comma separated Sketchfab tags, e.g. bed,military")
    search.add_argument("--max-faces", type=_positive_int, default=None, metavar="N",
                        help="only models with faceCount <= N (sent as max_face_count)")
    search.add_argument("--limit", type=_positive_int, default=10, metavar="N",
                        help="maximum number of models to print (default: 10)")
    search.add_argument("--license", default=None, metavar="SLUG",
                        help="restrict by Sketchfab license slug, e.g. cc0")
    search.add_argument("--sort", default=None, metavar="ORDER",
                        help="API sort order, e.g. -likeCount or faceCount")
    search.add_argument("--endpoint", default="/search", choices=["/search", "/models"],
                        help="Sketchfab index to query (default: /search)")
    search.set_defaults(func=cmd_search)

    likes = subparsers.add_parser(
        "likes", help="list models liked by the token owner (/v3/me/likes)")
    likes.add_argument("--limit", type=_positive_int, default=24, metavar="N",
                       help="maximum number of liked models to print (default: 24)")
    likes.add_argument("--cc0", action="store_true",
                       help="only keep CC0 / public domain models")
    likes.add_argument("--downloadable", action="store_true",
                       help="only keep models that allow downloads")
    likes.set_defaults(func=cmd_likes)

    thumb = subparsers.add_parser(
        "thumb", help="save the largest thumbnail of a model (preview only)")
    thumb.add_argument("uid", help="model uid from the search output")
    thumb.add_argument("--out", default="Temp/sketchfab_thumb.jpg", metavar="FILE",
                       help="output image path (default: %(default)s)")
    thumb.set_defaults(func=cmd_thumb)

    info = subparsers.add_parser(
        "info", help="print license, poly count, tags and GLB availability")
    info.add_argument("uid", help="model uid from the search output")
    info.set_defaults(func=cmd_info)

    download = subparsers.add_parser(
        "download", help="download the single-file .glb archive of a model")
    download.add_argument("uid", help="model uid from the search output")
    download.add_argument("--out", default=str(DEFAULT_OUT_DIR), metavar="DIR",
                          help="target directory (default: %(default)s)")
    download.add_argument("--force", action="store_true",
                          help="re-download even if the .glb already exists")
    download.set_defaults(func=cmd_download)
    return parser


def _configure_streams() -> None:
    """Keeps Unicode model names printable on legacy Windows consoles."""
    for stream in (sys.stdout, sys.stderr):
        reconfigure = getattr(stream, "reconfigure", None)
        if reconfigure is None:
            continue
        try:
            reconfigure(encoding="utf-8", errors="replace")
        except (ValueError, OSError):
            pass


def main(argv: Optional[List[str]] = None) -> int:
    _configure_streams()
    parser = build_parser()
    args = parser.parse_args(argv)
    try:
        return args.func(args)
    except FormatUnavailable as exc:
        print("error: {0}".format(exc), file=sys.stderr)
        return EXIT_NOT_AVAILABLE
    except SketchfabError as exc:
        print("error: {0}".format(exc), file=sys.stderr)
        return EXIT_ERROR
    except KeyboardInterrupt:
        print("interrupted", file=sys.stderr)
        return 130


if __name__ == "__main__":
    sys.exit(main())
