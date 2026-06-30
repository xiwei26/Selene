# Agent Notes

## Backend

The Selene backend is now based on SzeMeng76/LunaTV:

https://github.com/SzeMeng76/LunaTV

Native clients should prefer the LunaTV server API surface when a user session
has a server URL. In particular, Douban category data should use:

- `/api/douban/categories?kind=movie&category=\u70ed\u95e8&type=\u5168\u90e8`
- `/api/douban/categories?kind=tv&category=tv&type=tv`
- `/api/douban/categories?kind=tv&category=show&type=show`
- `/api/douban/categories?kind=tv&category=tv&type=tv_animation`

Do not assume the old direct Douban mobile endpoints are the primary backend.

## Packaging After Changes

After modifying native client code, rebuild/repackage the affected native app
before handing the work back. For native Windows changes, run the Windows
packaging flow after tests pass so `native-windows/publish/win-x64` reflects the
latest source.
