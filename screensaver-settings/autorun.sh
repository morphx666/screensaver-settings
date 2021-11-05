#! /bin/sh

####  Generate $APPNAME.desktop.
APPNAME="Screensaver-Settings"
EXENAME="screensaver-settings.exe"
ICONNAME=icon.png

cd $(dirname "$0")
APPDIR="$PWD"
EXEC="mono $APPDIR/$EXENAME"
ICON="$APPDIR/$ICONNAME"

sed -i -e "s@^Icon=.*@Icon=$ICON@" \
    -e "s@^Exec.*@Exec=$EXEC@"  "$APPNAME.desktop"
    
mv -f "$APPNAME.desktop" ~/Desktop