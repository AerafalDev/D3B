fn main() {
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
