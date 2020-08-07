rid=$1
ver=$2
mkdir ./output
id=$( docker create dodopizza/mysql-data-mover-platforms:latest )
docker cp $id:/output/ ./output
docker rm -v $id
ls -l ./output
zip -r ./output/mysql-data-mover_$ver_$rid.zip ./output/$rid