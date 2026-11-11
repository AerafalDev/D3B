# Changelog

## [0.13.0](https://github.com/AerafalDev/D3B/compare/v0.12.0...v0.13.0) (2026-11-11)


### Features

* **relay:** split the game connection into frames both ways as it is copied ([#155](https://github.com/AerafalDev/D3B/issues/155)) ([201dc88](https://github.com/AerafalDev/D3B/commit/201dc886054ff839521136ebd2d065ed6377f0e0))

## [0.12.0](https://github.com/AerafalDev/D3B/compare/v0.11.0...v0.12.0) (2026-11-11)


### Features

* **relay:** frames and a protobuf wire walker ([#142](https://github.com/AerafalDev/D3B/issues/142)) ([c1d7de4](https://github.com/AerafalDev/D3B/commit/c1d7de4432e9d23a120362fc5351a1e2a2038863))
* **relay:** read the client's version file and Ankama's configuration, and write their redirected copies ([#139](https://github.com/AerafalDev/D3B/issues/139)) ([4e9ae67](https://github.com/AerafalDev/D3B/commit/4e9ae67d06dd69ec256545242d46e20f1aaef612))
* **relay:** redirect the client's ConfigUrl to D3B's copy, and put it back ([#141](https://github.com/AerafalDev/D3B/issues/141)) ([29a76a1](https://github.com/AerafalDev/D3B/commit/29a76a11f13deeab0f2d6d3bd10861e3fdcb2eff))
* **relay:** relay the connection phase both ways ([#145](https://github.com/AerafalDev/D3B/issues/145)) ([19c922b](https://github.com/AerafalDev/D3B/commit/19c922b2fb0b5a5485d9a85adb9643e96c92fe06))
* **relay:** relay the game connection to the server its ticket names ([#146](https://github.com/AerafalDev/D3B/issues/146)) ([7f5ff41](https://github.com/AerafalDev/D3B/commit/7f5ff41eb38037834b82d91503a128c456b26e34))
* **relay:** rewrite the server-selection answer to the relay's game port ([#144](https://github.com/AerafalDev/D3B/issues/144)) ([a848df3](https://github.com/AerafalDev/D3B/commit/a848df3a6f0edb2aea685f1b3f9dc0d9973c403e))
* **relay:** start the relay with the app and put the client back at quit ([#148](https://github.com/AerafalDev/D3B/issues/148)) ([77f7320](https://github.com/AerafalDev/D3B/commit/77f73208a27f28c0b90e4c3a156c19361daeb7f6))
* **relay:** the relay in the status bar, and the client put back before an update ([#149](https://github.com/AerafalDev/D3B/issues/149)) ([4fa724f](https://github.com/AerafalDev/D3B/commit/4fa724fcadd11bfd6d23f6c0a4fd2d541a4ea024))
* **relay:** the tickets waiting for their game connection ([#143](https://github.com/AerafalDev/D3B/issues/143)) ([0c9fbde](https://github.com/AerafalDev/D3B/commit/0c9fbdef4857c5d2680aabdfedda3f4f4d0529e7))

## [0.11.0](https://github.com/AerafalDev/D3B/compare/v0.10.0...v0.11.0) (2026-11-11)


### Features

* **data:** the client-&lt;build&gt;.tar.gz archive, and what the review of step 1 found ([#135](https://github.com/AerafalDev/D3B/issues/135)) ([f3104e9](https://github.com/AerafalDev/D3B/commit/f3104e91a4eac11dadb8fc2cc0805536faf8e89a))
* **data:** the client's tables, texts and flag enums, and the world's tables the app reads ([#133](https://github.com/AerafalDev/D3B/issues/133)) ([c4ac88b](https://github.com/AerafalDev/D3B/commit/c4ac88b6f0ced39e0a987253dd9a88bd75c415e7))
* **data:** the worlds and their tiles, the maps, and the world graph ([#134](https://github.com/AerafalDev/D3B/issues/134)) ([d23c3c7](https://github.com/AerafalDev/D3B/commit/d23c3c79b9e311abd6fb66ddc5c7ac51763170bd))
* **extractor:** find the client, write the data folder, read objects through their type trees ([#132](https://github.com/AerafalDev/D3B/issues/132)) ([7a68f3c](https://github.com/AerafalDev/D3B/commit/7a68f3c0d316b43a38ef828fe62d12c6900ee775))

## [0.10.0](https://github.com/AerafalDev/D3B/compare/v0.9.1...v0.10.0) (2026-11-11)


### ⚠ BREAKING CHANGES

* start the Rust and Tauri rewrite on next

### Features

* **app:** update the app in the background from the next channel ([#114](https://github.com/AerafalDev/D3B/issues/114)) ([318c791](https://github.com/AerafalDev/D3B/commit/318c791d8d44dd600bd8e51913b9968ed938cdc0))
* start the Rust and Tauri rewrite on next ([f0d7ff9](https://github.com/AerafalDev/D3B/commit/f0d7ff99963abcd0a64623a5b4aed87e7d96c203))
