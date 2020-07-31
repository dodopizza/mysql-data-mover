#!/bin/bash
BASE_DIR=$(cd $(dirname $0); pwd)

exec ${BASE_DIR}/integration-tests/test.sh $@