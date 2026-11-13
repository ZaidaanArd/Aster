//! Windows integration for the Aster HUD.
//!
//! This module is only compiled on Windows. It subclasses the GPUI window so
//! the transparent area is click-through, the pill can be clicked without
//! activating the app, and the window stays out of the taskbar and Alt+Tab.

use std::ffi::c_void;
use std::sync::atomic::{AtomicIsize, AtomicU32, Ordering};

use raw_window_handle::{HasWindowHandle, RawWindowHandle};
use windows::Win32::Foundation::{HWND, LPARAM, LRESULT, POINT, RECT, WPARAM};
use windows::Win32::UI::WindowsAndMessaging::{
    CallWindowProcW, DefWindowProcW, GetCursorPos, GetWindowLongPtrW, GetWindowRect,
    GWL_EXSTYLE, GWL_WNDPROC, HTCLIENT, HTTRANSPARENT, MA_NOACTIVATE, SWP_FRAMECHANGED,
    SWP_NOACTIVATE, SWP_NOMOVE, SWP_NOSIZE, SWP_NOZORDER, SetWindowLongPtrW, SetWindowPos,
    WM_MOUSEACTIVATE, WM_NCHITTEST, WNDPROC, WS_EX_NOACTIVATE, WS_EX_TOOLWINDOW,
};

pub use super::hit_test;

static PREV_WNDPROC: AtomicIsize = AtomicIsize::new(0);
static SCALE_BITS: AtomicU32 = AtomicU32::new(0);

/// Applies the HUD window styles and installs the hit-test subclass.
///
/// Call this once, from inside the window-creation closure, after GPUI has
/// created the native window. The single-window spike keeps the subclass state
/// in statics.
pub fn install(window: &impl HasWindowHandle, scale: f32) {
    let handle = window.window_handle().expect("window handle");
    let RawWindowHandle::Win32(win32) = handle.as_raw() else {
        return;
    };
    let hwnd = HWND(win32.hwnd.get() as *mut c_void);
    SCALE_BITS.store(scale.to_bits(), Ordering::Relaxed);

    unsafe {
        let ex_style = GetWindowLongPtrW(hwnd, GWL_EXSTYLE);
        SetWindowLongPtrW(
            hwnd,
            GWL_EXSTYLE,
            ex_style | (WS_EX_NOACTIVATE.0 | WS_EX_TOOLWINDOW.0) as isize,
        );
        let _ = SetWindowPos(
            hwnd,
            None,
            0,
            0,
            0,
            0,
            SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE,
        );
        let previous = SetWindowLongPtrW(hwnd, GWL_WNDPROC, wndproc as *const () as isize);
        PREV_WNDPROC.store(previous, Ordering::Relaxed);
    }
}

unsafe extern "system" fn wndproc(
    hwnd: HWND,
    msg: u32,
    wparam: WPARAM,
    lparam: LPARAM,
) -> LRESULT {
    unsafe {
        match msg {
            WM_MOUSEACTIVATE => LRESULT(MA_NOACTIVATE as isize),
            WM_NCHITTEST => {
                let mut rect = RECT::default();
                let mut point = POINT::default();
                let _ = GetWindowRect(hwnd, &mut rect);
                let _ = GetCursorPos(&mut point);
                let width = rect.right - rect.left;
                let height = rect.bottom - rect.top;
                let x = point.x - rect.left;
                let y = point.y - rect.top;
                let scale = f32::from_bits(SCALE_BITS.load(Ordering::Relaxed));
                LRESULT(if hit_test(x, y, width, height, scale) {
                    HTCLIENT as isize
                } else {
                    HTTRANSPARENT as isize
                })
            }
            _ => {
                let previous = PREV_WNDPROC.load(Ordering::Relaxed);
                if previous == 0 {
                    DefWindowProcW(hwnd, msg, wparam, lparam)
                } else {
                    let previous: WNDPROC = std::mem::transmute(previous);
                    CallWindowProcW(previous, hwnd, msg, wparam, lparam)
                }
            }
        }
    }
}
