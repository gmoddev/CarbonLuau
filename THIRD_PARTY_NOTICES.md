# Third-party notices

## Luau

This repository vendors Luau under [`native/third_party/luau`](native/third_party/luau). Luau is maintained by Roblox Corporation and is licensed under the MIT License. See the vendored `LICENSE.txt` for the applicable notice.

## SQLite

The private storage worker statically includes SQLite 3.53.4, source ID
`2026-07-24 19:02:57 bf7c7f30031888f4e796e429ab3978879485813aaca6f641c7b33e4e09459bcc`.
SQLite is in the public domain. The verified upstream amalgamation is fetched
only at build time or read from an explicitly supplied verified source cache;
no system SQLite, SQLite CLI, runtime download or vendor source is shipped.
The archive and source hashes are pinned in
[`native/cmake/SQLite.cmake`](native/cmake/SQLite.cmake).
