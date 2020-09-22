rid=$1
id=$( docker create dodopizza/mysql-data-mover-platforms:latest )
mkdir -p ./mysql-data-mover
docker cp $id:/output/${rid} ./mysql-data-mover/${rid}
docker rm -v $id
ls -l ./mysql-data-mover/${rid}
zip -r ./mysql-data-mover/mysql-data-mover_${rid}.zip ./mysql-data-mover/${rid}
