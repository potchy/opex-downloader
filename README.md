# opex-downloader
Downloads whole One Piece seasons from OPEX. Requires geckodriver.

# options
```
--firefox-location <firefox-location>                    Path to the Firefox executable.
--season-url <season-url>                                OPEX season URL to download. Example:
                                                         https://onepieceex.net/episodios/t15/
--check-download-directory <check-download-directory>    Directory where you store all your One Piece episodes.
                                                         Example: "C:\One Piece"
--actual-download-directory <actual-download-directory>  Subdirectory where the specified season will be downloaded.
                                                         Example: "C:\One Piece\Season 21"
```

# example usage (PowerShell)
```PowerShell
.\OpexDownloader.exe ^
  --firefox-location "C:\Program Files\WindowsApps\Mozilla.Firefox_114.0.2.0_x64__n80bbvh6b1yt2\VFS\ProgramFiles\Firefox Package Root\firefox.exe" ^
  --season-url https://onepieceex.net/episodios/t15/ ^
  --check-download-directory "C:\One Piece" ^
  --actual-download-directory "C:\One Piece\Season 21" ^
```
