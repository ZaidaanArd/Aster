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
                let _ = window;
                cx.new(|_| Hud)
            },
        )
        .expect("open GPUI window");

        if let Ok(path) = std::env::var("ASTER_READY_FILE") {
            let _ = std::fs::write(path, started.elapsed().as_millis().to_string());
        }
    });
}
