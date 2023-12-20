#!/bin/bash

set -euo pipefail

SCRIPT_DIR=$(
    cd $(dirname $0)
    pwd
) # without ending /
REPO_DIR=$(
    cd $(dirname $0)/..
    pwd
) # without ending /

MYSQL_ROOT_PASSWORD='mover'
MYSQL_SRC='mysql-data-mover-src'
MYSQL_DST='mysql-data-mover-dst'
DATAMOVER_APP='mysql-data-mover-app'
DATAMOVER_IMAGE_TAG=''

#

# Usage: utils::get_rand <min> <max>
function utils::get_rand() {
    seq ${1} ${2} | sort -R | head -n 1
}

# Usage: relative_to_full <dir_path>
function relative_to_full() {
    echo $(
        cd "$(dirname "$1")"
        pwd
    )/$(basename "$1")
}

# Usage: app::process_parameters $@
function app::process_parameters() {
    while getopts "t:" opt; do
        case ${opt} in
        t) DATAMOVER_IMAGE_TAG=${OPTARG} ;;
        \?) echo -e "Usage: $(basename $0) [options]\n\t -t Set image tag" && exit 1 ;;
        esac
    done
}

# Usage: app::kill_all_subprocesses
function app::kill_all_subprocesses() {
    jobs -r -p | xargs kill &>/dev/null || true
}

# Usage: app::cleanup
function app::cleanup() {
    docker-compose down --volumes
}

# Usage: app::on_exit
function app::on_exit() {
    app::cleanup
    app::kill_all_subprocesses
}

# Usage mysql::is_ready <mysql_container_name>
function mysql::is_ready() {
    docker exec $1 mysql -u root -p${MYSQL_ROOT_PASSWORD} -e "EXIT" 2>/dev/null && return 0 || return $?
}

# Usage mysql:wait_for_start <mysql_container_name>
function mysql:wait_for_start() {
    echo -n "[~] Wait for mysql to initialize ($1) "
    while (! mysql::is_ready $1); do
        echo -n '.'
        sleep 2
    done
    echo
}

# Usage: mysql::start_haos_monkey <mysql_container_name>
function mysql::start_haos_monkey() {
    local service_name=${1}
    while :; do
        docker-compose stop ${service_name}
        sleep $(utils::get_rand 0 15)
        docker-compose start ${service_name}
        sleep $(utils::get_rand 30 60)
    done
}

# Usage: mysql::stop_haos_monkey
function mysql::stop_haos_monkey() {
    app::kill_all_subprocesses
}

# Usage: mysql::start_and_wait <mysql_container_name>
function mysql::start_and_wait() {
    docker-compose start ${1}
    mysql:wait_for_start ${1}
}

# Usage: mysql::wait_for_init_data
function mysql::wait_for_init_data() {
    echo "[~] Wait for init data"
    until docker exec -e MYSQL_PWD=ready "$1" mysql -uready -e "EXIT"
    do sleep 5; done
}

# Usage: mysql::get_count <mysql_container_name> <table>
function mysql::get_count() {
    docker exec -i ${1} mysql -NB -p${MYSQL_ROOT_PASSWORD} -e "SELECT COUNT(*) FROM ${2};" 2>/dev/null || echo 0
}

# Usage: mysql::table_diff <description> <src_table> <dst_table>
function mysql::table_diff() {
    local src_count=$(mysql::get_count ${MYSQL_SRC} "${2}")
    local dst_count=$(mysql::get_count ${MYSQL_DST} "${3}")
    echo -e "[ ] ${1}:\n\tsrc: ${src_count} dst: ${dst_count}"
    if [ "${src_count}" -ne "${dst_count}" ]; then
        echo "[E] Src and Dst counts are not equal"
        exit 1
    fi
}

# Usage: main::setup $@
function main::setup() {
    cd ${SCRIPT_DIR}
    app::process_parameters $@
    app::cleanup

    if [ -z "${DATAMOVER_IMAGE_TAG}" ]; then
        ${REPO_DIR}/run-build-image.sh rmi integration-test
        ${REPO_DIR}/run-build-image.sh build integration-test
    fi

    docker-compose up -d --wait
    mysql::wait_for_init_data ${MYSQL_SRC}
    mysql::start_haos_monkey ${MYSQL_SRC} &
    mysql::start_haos_monkey ${MYSQL_DST} &
}

# Usage: main::run_data_mover
function main::run_data_mover() {
    echo "[~] Run mysql-data-mover container app"
    docker run --rm \
        --name ${DATAMOVER_APP} \
        --network host \
        -e ASPNETCORE_ENVIRONMENT=integration-test \
        "dodopizza/mysql-data-mover:${DATAMOVER_IMAGE_TAG:-integration-test}"
}

# Usage: main::assert_tables_data_is_equal
function main::assert_tables_data_is_equal() {
    echo "[~] Verify copied data"
    mysql::table_diff 'table_with_composite_pk' 'test_db.table_with_composite_pk' 'test_db2.table_with_composite_pk'
    mysql::table_diff 'table_without_pk' 'test_db.table_without_pk' 'test_db2.table_without_pk'
}

# Usage: main $@
function main() {
    echo "[.] Start"
    # Arrange
    trap app::on_exit EXIT
    main::setup $@
    # Act
    main::run_data_mover
    mysql::stop_haos_monkey
    mysql::start_and_wait ${MYSQL_SRC}
    mysql::start_and_wait ${MYSQL_DST}
    # Assert
    main::assert_tables_data_is_equal
    echo "[.] Done"
}

main $@
