mod platform;

use std::time::Instant;

use gpui::{
    App, Bounds, Context, Window, WindowBackgroundAppearance, WindowBounds, WindowKind,
    WindowOptions, div, point, prelude::*, px, rgb, size,
};
use gpui_platform::application;

struct Hud;

impl Render for Hud {
    fn render(&mut self, _window: &mut Window, _cx: &mut Context<Self>) -> impl IntoElement {
        div()
            .flex()
            .justify_center()
            .items_start()
            .size_full()
            .child(
                div()
                    .flex()
                    .justify_center()
                    .items_center()
                    .w(px(300.))
                    .h(px(38.))
                    .rounded(px(19.))
                    .bg(rgb(0x0A0A0D))
                    .text_color(rgb(0xF5F4F0))
                    .text_sm()
                    .child("12:34"),
            )
    }
}

fn main() {
    let started = Instant::now();

    application().run(move |cx: &mut App| {
        let display = cx.primary_display().expect("primary display");
        let display_bounds = display.bounds();
        let width = 760.0;
        let height = 460.0;
        let left =
            display_bounds.origin.x.as_f32() + (display_bounds.size.width.as_f32() - width) / 2.0;
        let bounds = Bounds::new(
            point(px(left), display_bounds.origin.y),
            size(px(width), px(height)),
        );

        cx.open_window(
            WindowOptions {
                window_bounds: Some(WindowBounds::Windowed(bounds)),
                titlebar: None,
                window_background: WindowBackgroundAppearance::Transparent,
                focus: false,
                show: true,
                kind: WindowKind::PopUp,
                is_movable: false,
                ..Default::default()
            },
            |window, cx| {
                #[cfg(target_os = "windows")]
                platform::windows::install(&*window, window.scale_factor());
                #[cfg(not(target_os = "windows"))]
                let _ = window;
                cx.new(|_| Hud)
            },
        )
        .expect("open GPUI window");

        #[cfg(target_os = "windows")]
        setup_tray();

        if let Ok(path) = std::env::var("ASTER_READY_FILE") {
            let _ = std::fs::write(path, started.elapsed().as_millis().to_string());
        }
    });
}

#[cfg(target_os = "windows")]
fn setup_tray() {
    use tray_icon::Icon;
    use tray_icon::TrayIconBuilder;
    use tray_icon::menu::{Menu, MenuItem};

    let menu = Menu::new();
    menu.append(&MenuItem::new("Show/Hide", true, None))
        .expect("menu item");
    menu.append(&MenuItem::new("Quit", true, None))
        .expect("menu item");

    let icon_path = concat!(env!("CARGO_MANIFEST_DIR"), "/assets/tray.ico");
    let icon = Icon::from_path(icon_path, None).expect("tray icon");
    let tray = TrayIconBuilder::new()
        .with_menu(Box::new(menu))
        .with_tooltip("Aster Lab GPUI")
        .with_icon(icon)
        .build()
        .expect("tray icon");

    // The tray must live for the process lifetime; GPUI's event loop has no
    // place to store it in this spike.
    std::mem::forget(tray);
}
