#!/bin/sh
# Regenerates every file in src/Akaneiro.Server/GameData from the client project.
# Run from the akaneiro-server folder:  sh tools/regen-gamedata.sh
set -e
ASSETS=../akaneiro-client/Assets
A=$ASSETS/Scenes/Areas
OUT=src/Akaneiro.Server/GameData
SCENES="$A/Hub_Village.unity $A/Hub_Village_Tutorial.unity $(ls $A/Area_*/CoreScenes/*.unity)"


dotnet build tools/SceneExtract >/dev/null
X="dotnet run --no-build --project tools/SceneExtract --"
$X scenes   $ASSETS $OUT/scenes.json   $SCENES
$X spawners $ASSETS $OUT/spawners.json $SCENES
$X triggers $ASSETS $OUT/triggers.json $SCENES
$X monsters $ASSETS $OUT/monsters.json $ASSETS/Prefabs/CH/Enemies
$X missions $ASSETS $OUT/missions.json $A
$X breakables $ASSETS $OUT/breakables.json $SCENES
