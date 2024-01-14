CONTAINER_REGISTRY_ID=$(terraform output -raw container_registry_id)
IMAGE_NAME="cr.yandex/${CONTAINER_REGISTRY_ID}/wall-app:latest"
SERVICE_ACCOUNT_ID=$(terraform output -raw service_account_id)
YDB_ENDPOINT=$(terraform output -raw ydb_endpoint)
YDB_DATABASE=$(terraform output -raw ydb_database)
YDB_SERVICE_ACCOUNT_KEY_CREDENTIALS=$(terraform output -raw ydb_service_account_key | base64 -w 0)

CURRENT_REPLICAS_COUNT=$(yc serverless container revision list --container-id $(yc serverless container get --name wall-app --format json | jq -r '.id') --format json | jq -r '[.[] | select(.status == "ACTIVE")] | if length == 0 then 0 else .[0].provision_policy.min_instances // 1 end')
NEW_REPLICAS_COUNT=$((CURRENT_REPLICAS_COUNT + 1))
echo $NEW_REPLICAS_COUNT

yc serverless container revision deploy --container-name wall-app \
  --image ${IMAGE_NAME} \
  --cores 1 \
  --memory 256M \
  --concurrency 1 \
  --execution-timeout 30s \
  --service-account-id ${SERVICE_ACCOUNT_ID} \
  --environment YDB_DATABASE=${YDB_DATABASE},YDB_ENDPOINT=${YDB_ENDPOINT},YDB_SERVICE_ACCOUNT_KEY_CREDENTIALS=${YDB_SERVICE_ACCOUNT_KEY_CREDENTIALS},BACKEND_VERSION=${CURRENT_BACKEND_VERSION} \
  --min-instances ${NEW_REPLICAS_COUNT}