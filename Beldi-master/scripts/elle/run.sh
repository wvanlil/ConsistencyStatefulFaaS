# read -p "Please Input request rate (default: 100): " rate
# rate=${rate:-100}
echo "Compiling"
make clean >/dev/null
make elle >/dev/null
echo "Deploying"
sls deploy -c elle.yml >/dev/null
# read -p "Please Input HTTP gateway url for beldi-dev-gateway: " bp
echo "Initializing Database"

go run ./internal/elle/init/init.go clean beldi
go run ./internal/elle/init/init.go create beldi
go run ./internal/elle/init/init.go populate beldi

# go run ./internal/elle/init/init.go populate beldi

echo "Ready to run tests"
# echo "Running beldi"
# ENDPOINT="$bp" ./tools/wrk -t4 -c$rate -d420s -R$rate -s ./benchmark/hotel/workload.lua --timeout 10s "$bp" >/dev/null
# echo "Collecting metrics"
# python ./scripts/hotel/hotel.py --command run --config beldi --duration 7
# echo "Cleanup"
# go run ./internal/hotel/init/init.go clean beldi
