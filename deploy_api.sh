CONTAINER_REGISTRY_ID=$(terraform output -raw container_registry_id)
CURRENT_BACKEND_VERSION="0.0.1"
IMAGE_NAME="cr.yandex/${CONTAINER_REGISTRY_ID}/wall-app:${CURRENT_BACKEND_VERSION}"
SERVICE_ACCOUNT_ID=$(terraform output -raw service_account_id)
YDB_ENDPOINT=$(terraform output -raw ydb_endpoint)
YDB_DATABASE=$(terraform output -raw ydb_database)
YDB_SERVICE_ACCOUNT_KEY_CREDENTIALS=$(terraform output -raw ydb_service_account_key | base64 -w 0)

docker build -t ${IMAGE_NAME} -f ./.docker/app.dockerfile ./App
docker push ${IMAGE_NAME}

yc serverless container revision deploy --container-name wall-app \
  --image ${IMAGE_NAME} \
  --cores 1 \
  --memory 256M \
  --concurrency 1 \
  --execution-timeout 30s \
  --service-account-id ${SERVICE_ACCOUNT_ID} \
  --environment YDB_DATABASE=${YDB_DATABASE},YDB_ENDPOINT=${YDB_ENDPOINT},YDB_SERVICE_ACCOUNT_KEY_CREDENTIALS=${YDB_SERVICE_ACCOUNT_KEY_CREDENTIALS},BACKEND_VERSION=${BACKEND_VERSION}