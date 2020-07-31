#!/bin/bash

set -eu

trap 'docker-compose -f ./integration-tests/docker-compose.yml down --volumes' SIGINT SIGTERM SIGQUIT EXIT ERR

docker-compose -f ./integration-tests/docker-compose.yml up