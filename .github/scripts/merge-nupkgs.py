#!/usr/bin/env python3
"""Merge per-platform partial .nupkg/.snupkg files into complete packages.

Each platform CI job builds and packs only the TargetFrameworks it can build
(dotnet pack --no-build with the same TFM-slice properties as the build), so a
package like Nalu.Maui.Core exists as three partial nupkgs whose lib/ folders
and nuspec dependency groups cover just that slice. This script unions them:

- payload files are copied from every partial, first input wins on duplicate
  paths (duplicates are the deliberately re-built plain/netstandard outputs,
  equivalent by construction: same commit, same version);
- the nuspec <dependencies> / <frameworkReferences> groups are merged by
  targetFramework attribute;
- [Content_Types].xml Default/Override entries are unioned;
- _rels/.rels and the psmdcp metadata part are taken from the first partial.

The partials must agree on package id and version (MinVer computes both from
the same commit on every runner) or the merge fails.

Usage: merge-nupkgs.py <output-dir> <input-dir> [<input-dir> ...]
"""

import os
import shutil
import sys
import xml.etree.ElementTree as ET
import zipfile


def fail(msg):
    print(f"merge-nupkgs: error: {msg}", file=sys.stderr)
    sys.exit(1)


def read_entries(zip_path):
    """Return {name: bytes} preserving the archive's entry order."""
    entries = {}
    with zipfile.ZipFile(zip_path) as z:
        for info in z.infolist():
            if not info.is_dir():
                entries[info.filename] = z.read(info.filename)
    return entries


def local_name(tag):
    return tag.rsplit("}", 1)[-1]


def element_ns(root):
    return root.tag[1:].rsplit("}", 1)[0] if root.tag.startswith("{") else ""


def parse_xml(data):
    return ET.fromstring(data)


def serialize_xml(root):
    ET.register_namespace("", element_ns(root))
    return ET.tostring(root, encoding="utf-8", xml_declaration=True)


def nuspec_identity(nuspec_root):
    ns = element_ns(nuspec_root)
    meta = nuspec_root.find(f"{{{ns}}}metadata")
    if meta is None:
        fail("nuspec has no <metadata> element")
    pkg_id = meta.findtext(f"{{{ns}}}id")
    version = meta.findtext(f"{{{ns}}}version")
    return pkg_id, version


def merge_nuspec(base_root, other_root):
    """Union per-targetFramework groups of <dependencies>/<frameworkReferences>."""
    ns = element_ns(base_root)
    base_meta = base_root.find(f"{{{ns}}}metadata")
    other_meta = other_root.find(f"{{{ns}}}metadata")
    if other_meta is None:
        return
    for container_name in ("dependencies", "frameworkReferences"):
        other_container = other_meta.find(f"{{{ns}}}{container_name}")
        if other_container is None:
            continue
        base_container = base_meta.find(f"{{{ns}}}{container_name}")
        if base_container is None:
            base_meta.append(other_container)
            continue
        seen = {g.get("targetFramework") for g in base_container if local_name(g.tag) == "group"}
        for group in other_container:
            if local_name(group.tag) == "group" and group.get("targetFramework") not in seen:
                base_container.append(group)


def merge_content_types(base_root, other_root):
    seen = set()
    for child in base_root:
        key = (local_name(child.tag), child.get("Extension"), child.get("PartName"))
        seen.add(key)
    for child in other_root:
        key = (local_name(child.tag), child.get("Extension"), child.get("PartName"))
        if key not in seen:
            base_root.append(child)
            seen.add(key)


def is_metadata_part(name):
    return (
        name == "[Content_Types].xml"
        or name == "_rels/.rels"
        or name.endswith(".nuspec")
        or (name.startswith("package/services/metadata/") and name.endswith(".psmdcp"))
    )


def merge_package(paths, out_path):
    base = read_entries(paths[0])
    base_nuspec_name = next((n for n in base if n.endswith(".nuspec")), None)
    ct_root = parse_xml(base["[Content_Types].xml"]) if "[Content_Types].xml" in base else None
    nuspec_root = parse_xml(base[base_nuspec_name]) if base_nuspec_name else None
    identity = nuspec_identity(nuspec_root) if nuspec_root is not None else None

    for path in paths[1:]:
        other = read_entries(path)
        other_nuspec_name = next((n for n in other if n.endswith(".nuspec")), None)
        if nuspec_root is not None and other_nuspec_name:
            other_nuspec = parse_xml(other[other_nuspec_name])
            if nuspec_identity(other_nuspec) != identity:
                fail(f"{os.path.basename(out_path)}: partials disagree on package id/version: "
                     f"{identity} vs {nuspec_identity(other_nuspec)} ({path})")
            merge_nuspec(nuspec_root, other_nuspec)
        if ct_root is not None and "[Content_Types].xml" in other:
            merge_content_types(ct_root, parse_xml(other["[Content_Types].xml"]))
        for name, data in other.items():
            if not is_metadata_part(name) and name not in base:
                base[name] = data

    if nuspec_root is not None:
        base[base_nuspec_name] = serialize_xml(nuspec_root)
    if ct_root is not None:
        base["[Content_Types].xml"] = serialize_xml(ct_root)

    with zipfile.ZipFile(out_path, "w", compression=zipfile.ZIP_DEFLATED) as z:
        for name, data in base.items():
            z.writestr(name, data)


def summarize(out_path):
    with zipfile.ZipFile(out_path) as z:
        names = z.namelist()
    tfms = sorted({n.split("/")[1] for n in names if n.startswith("lib/") and n.count("/") >= 2})
    extras = sorted({n.split("/")[0] for n in names
                     if "/" in n and n.split("/")[0] not in ("lib", "_rels", "package")})
    parts = []
    if tfms:
        parts.append(f"lib: {', '.join(tfms)}")
    if extras:
        parts.append(f"also: {', '.join(extras)}")
    print(f"  {os.path.basename(out_path)}: {'; '.join(parts) if parts else 'content-only'}")


def main():
    if len(sys.argv) < 3:
        fail(f"usage: {sys.argv[0]} <output-dir> <input-dir> [<input-dir> ...]")
    out_dir, in_dirs = sys.argv[1], sys.argv[2:]
    for d in in_dirs:
        if not os.path.isdir(d):
            fail(f"input directory not found: {d}")
    os.makedirs(out_dir, exist_ok=True)

    packages = {}
    for d in in_dirs:
        for f in sorted(os.listdir(d)):
            if f.endswith((".nupkg", ".snupkg")):
                packages.setdefault(f, []).append(os.path.join(d, f))
    if not packages:
        fail(f"no .nupkg/.snupkg files found under: {', '.join(in_dirs)}")

    print(f"Merging {len(packages)} package(s) from {len(in_dirs)} input(s):")
    for name, paths in sorted(packages.items()):
        out_path = os.path.join(out_dir, name)
        if len(paths) == 1:
            shutil.copyfile(paths[0], out_path)
        else:
            merge_package(paths, out_path)
        summarize(out_path)


if __name__ == "__main__":
    main()
