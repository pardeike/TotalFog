#!/bin/sh
exec "$(dirname "$0")/mod" "${1:-build}"
