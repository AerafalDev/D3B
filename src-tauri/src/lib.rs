//! D3B's back end. Each folder is one domain (`data`, then `relay`, `protocol`, `game`, `bot`, `storage` as they come),
//! and `ipc` is the only one the webview talks to.

mod bot;
mod data;
mod game;
mod ipc;
mod protocol;
mod relay;
mod storage;

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
        // The window's state comes back at each start, but not its frame: it would restore the system frame a former
        // version saved over the frameless window Windows now has (`tauri.windows.conf.json`, docs/shell.md).
        let restored =
            tauri_plugin_window_state::StateFlags::all() - tauri_plugin_window_state::StateFlags::DECORATIONS;
        app = app.plugin(tauri_plugin_updater::Builder::new().build()).plugin(
            tauri_plugin_window_state::Builder::new()
                .with_state_flags(restored)
                .build(),
        );
    }

    app.plugin(tauri_plugin_log::Builder::new().level(log_level()).build())
        .plugin(tauri_plugin_opener::init())
        .plugin(tauri_plugin_process::init())
        .invoke_handler(ipc.invoke_handler())
        .setup(move |app| {
            use tauri::Manager;
            app.manage(ipc::LoadedData::load(app.handle()));
            app.manage(ipc::open_store(app.handle()));
            ipc.mount_events(app);
            ipc::start_relay(app);
            Ok(())
        })
        .build(tauri::generate_context!())
        .unwrap_or_else(|error| panic!("the app could not start: {error}"))
        .run(|app, event| {
            // The client's configuration goes back to Ankama's whichever way the app ends but a crash, which the next
            // start recovers from (docs/relay.md).
            if let tauri::RunEvent::Exit = event {
                ipc::stop_relay(app);
            }
        });
}

fn log_level() -> tauri_plugin_log::log::LevelFilter {
    if cfg!(debug_assertions) {
        tauri_plugin_log::log::LevelFilter::Debug
    } else {
        tauri_plugin_log::log::LevelFilter::Info
    }
}
