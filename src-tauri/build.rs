fn main() {
    compile_protocol();
    // On Windows, tauri-build embeds its manifest in the app's binary only, so `cargo test` binaries start without
    // Common Controls 6 and die with STATUS_ENTRYPOINT_NOT_FOUND. The manifest is linked into every binary instead.
    let target_os = std::env::var("CARGO_CFG_TARGET_OS").unwrap_or_default();
    let target_env = std::env::var("CARGO_CFG_TARGET_ENV").unwrap_or_default();
    let windows = if target_os == "windows" && target_env == "msvc" {
        let manifest = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("windows-app-manifest.xml");
        println!("cargo:rerun-if-changed={}", manifest.display());
        println!("cargo:rustc-link-arg=/MANIFEST:EMBED");
        println!("cargo:rustc-link-arg=/MANIFESTINPUT:{}", manifest.display());
        tauri_build::WindowsAttributes::new_without_app_manifest()
    } else {
        tauri_build::WindowsAttributes::new()
    };
    tauri_build::try_build(tauri_build::Attributes::new().windows_attributes(windows))
        .unwrap_or_else(|error| panic!("tauri-build failed: {error:#}"));
}

/// Compiles our readable `.proto` files of the game protocol (`protocol/game/`, docs/protocol.md) with protox, so that
/// no `protoc` needs installing, into the `d3b.game` module `src/protocol/` includes.
fn compile_protocol() {
    let root = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("protocol");
    let folder = root.join("game");
    println!("cargo:rerun-if-changed={}", folder.display());
    let mut files: Vec<_> = std::fs::read_dir(&folder)
        .unwrap_or_else(|error| panic!("{}: {error}", folder.display()))
        .filter_map(|entry| entry.ok().map(|entry| entry.path()))
        .filter(|path| path.extension().is_some_and(|extension| extension == "proto"))
        .collect();
    // In a fixed order, so that the generated code never depends on the folder's.
    files.sort();
    let descriptors = protox::compile(&files, [&root]).unwrap_or_else(|error| panic!("protox: {error:?}"));
    prost_build::Config::new()
        .compile_fds(descriptors)
        .unwrap_or_else(|error| panic!("prost-build: {error}"));
}
