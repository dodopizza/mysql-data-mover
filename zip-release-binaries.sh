rid=$1
id=$( docker create dodopizza/mysql-data-mover-platforms:latest )
mkdir -p ./output
docker cp $id:/output/${rid} ./output/${rid}
docker rm -v $id
ls -l ./output/${rid}
zip -r ./output/mysql-data-mover_${rid}.zip ./output/${rid}