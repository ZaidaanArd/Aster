#[cfg(windows)]
fn main() -> windows::core::Result<()> {
    use windows::Media::Control::GlobalSystemMediaTransportControlsSessionManager;

    let manager = GlobalSystemMediaTransportControlsSessionManager::RequestAsync()?.get()?;
    let sessions = manager.GetSessions()?;

    if sessions.Size()? == 0 {
        println!("NO_SESSION");
        return Ok(());
    }

    let session = manager.GetCurrentSession()?;
    let props = session.TryGetMediaPropertiesAsync()?.get()?;
    println!("{}\t{}\t{}", props.Title()?, props.Artist()?, props.AlbumTitle()?);
    Ok(())
}

#[cfg(not(windows))]
fn main() {
    println!("NO_SESSION (Windows-only probe)");
}
