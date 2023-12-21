#!/bin/bash
set -eu

SCRIPT_DIR=$(
    cd $(dirname $0)
    pwd
) # without ending /
cd ${SCRIPT_DIR}

function usage() { echo "Usage: $(basename $0) <build|build-no-cache|test|push|rmi> [tag = current branch] [message]" && exit 1; }
[ $# -lt 1 ] && usage

repo=dodopizza/mysql-data-mover
current_branch=$(git branch | grep \* | cut -d ' ' -f2)

action=${1:-'build'}
tag=${2:-${current_branch}}
message=${1:-"${tag}"}

echo "[~] Tag '${tag}'"

case "${action}" in
build)
    DOCKER_BUILDKIT=1 docker build --rm \
        --progress=plain \
        --build-arg BUILDKIT_INLINE_CACHE=1 \
        --cache-from ${repo}:cache__ \
        --tag ${repo}:${tag} \
        --tag ${repo}:cache__ \
        --file Dockerfile \
        .
    ;;

build-no-cache)
    DOCKER_BUILDKIT=1 docker build --rm \
        --progress=plain \
        --no-cache \
        --tag ${repo}:${tag} \
        --file Dockerfile \
        .
    ;;
push)
    docker push ${repo}:${tag}
    ;;
test)
    docker run -it --rm ${repo}:${tag}
    ;;
rmi)
    docker rmi -f ${repo}:${tag}
    ;;
*)
    usage
    ;;
esac

echo "[.] All Done"
