//! D3B's back end. Each folder is one domain (`data`, then `relay`, `protocol`, `game`, `bot`, `storage` as they come),
//! and `ipc` is the only one the webview talks to.

mod data;
mod ipc;
#[cfg_attr(
    not(test),
    expect(
        dead_code,
        reason = "the app starts the relay from the last pull request of its stack"
    )
)]
mod relay;

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    let ipc = ipc::builder();

    let mut app = tauri::Builder::default();

    #[cfg(desktop)]
    {
        // A second launch focuses the running window instead of opening a second relay on the same ports.
        app = app.plugin(tauri_plugin_single_instance::init(|app, _args, _cwd| {
            use tauri::Manager;
            if let Some(window) = app.get_webview_window("main") {
                let _ = window.unminimize();
                let _ = window.set_focus();
            }
        }));
        app = app
            .plugin(tauri_plugin_updater::Builder::new().build())
            .plugin(tauri_plugin_window_state::Builder::new().build());
    }

    app.plugin(tauri_plugin_log::Builder::new().level(log_level()).build())
        .plugin(tauri_plugin_opener::init())
        .plugin(tauri_plugin_process::init())
        .invoke_handler(ipc.invoke_handler())
        .setup(move |app| {
            use tauri::Manager;
            app.manage(ipc::LoadedData::load(app.handle()));
            ipc.mount_events(app);
            Ok(())
        })
        .run(tauri::generate_context!())
        .unwrap_or_else(|error| panic!("the app could not start: {error}"));
}

fn log_level() -> tauri_plugin_log::log::LevelFilter {
    if cfg!(debug_assertions) {
        tauri_plugin_log::log::LevelFilter::Debug
    } else {
        tauri_plugin_log::log::LevelFilter::Info
    }
}
