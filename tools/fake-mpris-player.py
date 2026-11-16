#!/usr/bin/env python3
import asyncio
from dbus_next.aio import MessageBus
from dbus_next.service import (
    ServiceInterface, method, dbus_property, PropertyAccess,
)
from dbus_next import Variant


class Player(ServiceInterface):
    def __init__(self):
        super().__init__("org.mpris.MediaPlayer2.Player")
        self.status = "Playing"
        self.track = 1
        self.position = 0

    @dbus_property(access=PropertyAccess.READ)
    def PlaybackStatus(self) -> "s":
        return self.status

    @dbus_property(access=PropertyAccess.READ)
    def Metadata(self) -> "a{sv}":
        return {
            "xesam:title": Variant("s", f"Tone {self.track}"),
            "xesam:artist": Variant("as", ["Aster Lab"]),
            "xesam:album": Variant("s", "Phase 2"),
            "mpris:length": Variant("x", 120_000_000),
            "mpris:trackid": Variant(
                "o", f"/org/mpris/MediaPlayer2/Track/{self.track}"),
            "mpris:artUrl": Variant("s", ""),
        }

    @dbus_property(access=PropertyAccess.READ)
    def Position(self) -> "x":
        return self.position

    @dbus_property(access=PropertyAccess.READ)
    def CanSeek(self) -> "b":
        return True

    @dbus_property(access=PropertyAccess.READ)
    def CanGoNext(self) -> "b":
        return True

    @dbus_property(access=PropertyAccess.READ)
    def CanGoPrevious(self) -> "b":
        return True

    @method()
    def PlayPause(self):
        self.status = "Paused" if self.status == "Playing" else "Playing"
        self.emit_properties_changed({"PlaybackStatus": self.status})

    @method()
    def Next(self):
        self.track += 1
        self.position = 0
        self.emit_properties_changed({"Metadata": self.Metadata})

    @method()
    def Previous(self):
        self.track = max(1, self.track - 1)
        self.position = 0
        self.emit_properties_changed({"Metadata": self.Metadata})

    @method()
    def SetPosition(self, track_id: "o", position: "x"):
        self.position = position
        self.emit_properties_changed({"Position": position})

    @method()
    def Seek(self, offset: "x"):
        self.position = max(0, self.position + offset)
        self.emit_properties_changed({"Position": self.position})


async def main():
    bus = await MessageBus().connect()
    bus.export("/org/mpris/MediaPlayer2", Player())
    await bus.request_name("org.mpris.MediaPlayer2.FakePlayer")
    await bus.wait_for_disconnect()


asyncio.run(main())
