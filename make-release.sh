#!/bin/bash

cd $(dirname "$0")

../../../PyKSPutils/make_mod_release \
-e '*.user' '*.orig' '*.mdb' '*.pdb' \
'GameData/000_AT_Utils/Plugins/AnimatedConverters.dll' \
'GameData/000_AT_Utils/Plugins/002_MultiAnimators.dll' \
'GameData/000_AT_Utils/Plugins/SubmodelResizer.dll' \
'GameData/000_AT_Utils/ResourceHack.cfg' \
-i '../GameData' \
-i 'IntrusivePatches' \
--dll ../GameData/000_AT_Utils/Plugins/ConfigurableContainers.dll

