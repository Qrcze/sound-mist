# Sound Mist
An unofficial desktop SoundCloud player for Windows and Linux, built from the ground-up.

## Working base features:
- Logging in with the auth token cookie.
- Getting your liked tracks list.
- Searching for tracks, artists and albums.
- Autoplay/stations.
- Basic info page about tracks, users and albums.
- Play history list.
- Playing music while it still downloads.
- Track looping modes

## Extra features:
- Blocking selected tracks/uploaders.
- Shuffling the queue works on stations/autoplay too.
- Downloading tracks to store locally.
- Local browsing and playing history.
- Light/Dark themes.
- Windows: System integration through SMTC.
- Linux: System integration through MPRIS D-Bus.
- System-wide media controls.
- No ads.
- Fetching all of the liked tracks in one go.

## Build instructions:
### For Windows:
```
dotnet publish --runtime win-x64 -p:PublishSingleFile=true --self-contained false
```
The files should end up in: `bin\Release\net8.0-windows10.0.19041.0\win-x64\publish`.

### For Linux:
```
dotnet publish --runtime linux-x64 -p:PublishSingleFile=true --self-contained false
```
The files should end up in: `bin\Release\net8.0\linux-x64\publish`.

---
![](/images/1.png)
![](/images/2.png)
![](/images/3.png)
