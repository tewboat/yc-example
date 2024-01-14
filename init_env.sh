export YC_TOKEN=$(yc iam create-token)
export YC_CLOUD_ID=$(yc config get cloud-id)
export YC_FOLDER_ID=$(yc config get folder-id)

terraform init
terraform apply -target yandex_container_registry.registry \
                                 -target yandex_ydb_database_serverless.app-ydb \
                                 -target yandex_iam_service_account.service_account \
                                 -target yandex_resourcemanager_folder_iam_member.roles \
                                 -target yandex_iam_service_account.ydb_service_account \
                                 -target yandex_resourcemanager_folder_iam_member.ydb_role \
                                 -target yandex_iam_service_account_key.ydb_service_account_key \
                                 -var="CONTAINER_ID="

yc container registry configure-docker
CONTAINER_ID=$(yc serverless container create --name wall-app --format json | jq -r '.id')

./deploy_api.sh

terraform apply -var="CONTAINER_ID=$CONTAINER_ID"

./deploy_front.sh