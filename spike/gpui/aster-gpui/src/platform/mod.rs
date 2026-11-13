//! Platform integration for the Aster HUD.
//!
//! The hit-test is deliberately platform-neutral so it can be unit-tested on
//! every host. Windows-only window hooks live in [`windows`].

/// Logical width of the collapsed pill.
pub const ISLAND_WIDTH: f32 = 300.0;

/// Logical height of the collapsed pill.
pub const ISLAND_HEIGHT: f32 = 38.0;

/// Returns whether a point belongs to the interactive pill.
///
/// `x` and `y` are physical client-area pixels, `window_width` and
/// `window_height` are the physical window size, and `scale` is the window's
/// scale factor. The pill is centered horizontally at the top of the window,
/// matching the layout rendered by `Hud::render`.
#[cfg_attr(not(target_os = "windows"), allow(dead_code))]
pub fn hit_test(x: i32, y: i32, window_width: i32, window_height: i32, scale: f32) -> bool {
    let island_width = (ISLAND_WIDTH * scale) as i32;
    let island_height = (ISLAND_HEIGHT * scale) as i32;
    let left = (window_width - island_width) / 2;
    let right = left + island_width;
    let bottom = island_height.min(window_height);

    x >= left && x < right && y >= 0 && y < bottom
}

#[cfg(target_os = "windows")]
pub mod windows;

#[cfg(test)]
mod tests {
    use super::hit_test;

    #[test]
    fn center_is_interactive_at_100_percent() {
        assert!(hit_test(380, 19, 760, 460, 1.0));
    }

    #[test]
    fn left_edge_is_transparent_at_100_percent() {
        assert!(!hit_test(10, 19, 760, 460, 1.0));
    }

    #[test]
    fn center_is_interactive_at_150_percent() {
        assert!(hit_test(570, 28, 1140, 690, 1.5));
    }

    #[test]
    fn center_is_interactive_at_125_percent() {
        // 760x460 logical becomes 950x575 physical at 125%.
        assert!(hit_test(475, 23, 950, 575, 1.25));
    }

    #[test]
    fn below_the_pill_is_transparent() {
        assert!(!hit_test(380, 39, 760, 460, 1.0));
    }

    #[test]
    fn right_of_the_pill_is_transparent() {
        assert!(!hit_test(531, 19, 760, 460, 1.0));
    }

    #[test]
    fn pill_edges_are_interactive() {
        // Pill spans [230, 530) x [0, 38) at 100%.
        assert!(hit_test(230, 0, 760, 460, 1.0));
        assert!(hit_test(529, 37, 760, 460, 1.0));
    }

    #[test]
    fn window_height_clamps_the_pill() {
        assert!(!hit_test(380, 30, 760, 20, 1.0));
    }
}
