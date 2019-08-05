#!/bin/bash

. "$(dirname ${BASH_SOURCE[0]})/InitializeEnvironment.sh"

CONFIGURATION=$1
if [ -z $CONFIGURATION ]; then
  CONFIGURATION=Debug
fi

mkdir ~/GVFS.FT
if [ "$2" != "--test-gvfs-on-path" ]; then
  echo "Calling LoadPrjFSKext.sh as --test-gvfs-on-path not set..."
  $VFS_SRCDIR/ProjFS.Mac/Scripts/LoadPrjFSKext.sh $CONFIGURATION
fi

if [ "$3" != "--sparse-mode" ]; then
  $VFS_PUBLISHDIR/GVFS.FunctionalTests --full-suite $2
else
  $VFS_PUBLISHDIR/GVFS.FunctionalTests $2 $3
fi
