#!/bin/sh
# The canonical workflow serializes builds/deploys and refuses a running game.
exec "$(dirname "$0")/mod" deploy-built
