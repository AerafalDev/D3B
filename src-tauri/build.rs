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
    describe_protocol!(descriptors);
    prost_build::Config::new()
        .compile_fds(descriptors)
        .unwrap_or_else(|error| panic!("prost-build: {error}"));
}

/// Writes `OUT_DIR/protocol-shapes.txt`, our messages as the pins' audit test compares them with a build's schema
/// (docs/protocol.md, "A new build"): `message <name>`, then `field <number> <kind> <label> <oneof> <type>` per field
/// (`-` for none), and `enum <name> <values>`. A macro, since the descriptors' types belong to a crate this script
/// does not name; our `.proto` files nest one level at most, and a deeper one fails the build.
macro_rules! describe_protocol {
    ($descriptors:expr) => {{
        use std::fmt::Write as _;
        let mut out = String::new();
        macro_rules! describe_message {
            ($message:expr, $name:expr) => {{
                let name: String = $name;
                writeln!(out, "message {name}").unwrap_or_default();
                for field in &$message.field {
                    let kind = match field.r#type.unwrap_or_default() {
                        1 => "double",
                        2 => "float",
                        3 => "int64",
                        4 => "uint64",
                        5 => "int32",
                        8 => "bool",
                        9 => "string",
                        11 => "message",
                        12 => "bytes",
                        13 => "uint32",
                        14 => "enum",
                        other => panic!("{name}: a field of protobuf type {other}, which the audit does not compare"),
                    };
                    let optional = field.proto3_optional.unwrap_or_default();
                    let label = match (field.label.unwrap_or_default(), optional) {
                        (3, _) => "repeated",
                        (_, true) => "optional",
                        _ => "-",
                    };
                    let oneof = match (field.oneof_index, optional) {
                        (Some(index), false) => $message
                            .oneof_decl
                            .get(usize::try_from(index).unwrap_or(usize::MAX))
                            .and_then(|oneof| oneof.name.clone())
                            .map_or_else(
                                || panic!("{name}: a oneof that is not declared"),
                                |oneof| format!("{name}.{oneof}"),
                            ),
                        _ => "-".to_owned(),
                    };
                    let of = field
                        .type_name
                        .as_deref()
                        .map_or("-", |of| of.trim_start_matches(".d3b.game."));
                    let number = field.number.unwrap_or_default();
                    writeln!(out, "field {number} {kind} {label} {oneof} {of}").unwrap_or_default();
                }
                for kind in &$message.enum_type {
                    let values: Vec<String> = kind
                        .value
                        .iter()
                        .map(|value| value.number.unwrap_or_default().to_string())
                        .collect();
                    let kind = kind.name.as_deref().unwrap_or_default();
                    writeln!(out, "enum {name}.{kind} {}", values.join(" ")).unwrap_or_default();
                }
            }};
        }
        for file in $descriptors
            .file
            .iter()
            .filter(|file| file.package.as_deref() == Some("d3b.game"))
        {
            for message in &file.message_type {
                let outer = message.name.clone().unwrap_or_default();
                describe_message!(message, outer.clone());
                for nested in &message.nested_type {
                    assert!(
                        nested.nested_type.is_empty(),
                        "{outer}: nested two levels down, which the audit does not read"
                    );
                    describe_message!(
                        nested,
                        format!("{outer}.{}", nested.name.as_deref().unwrap_or_default())
                    );
                }
            }
        }
        let path = std::path::Path::new(&std::env::var("OUT_DIR").unwrap_or_default()).join("protocol-shapes.txt");
        std::fs::write(&path, out).unwrap_or_else(|error| panic!("{}: {error}", path.display()));
    }};
}
use describe_protocol;
