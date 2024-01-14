terraform {
  required_providers {
    yandex = {
      source = "yandex-cloud/yandex"
    }
  }
}

provider "yandex" {
  zone = "ru-central1-a"
}

variable "CONTAINER_ID" {
  type    = string
}

resource "yandex_api_gateway" "gateway" {
  name = "wall-api-gateway"

  spec = templatefile("spec.yaml", {
    SERVICE_ACCOUNT_ID      = yandex_iam_service_account.service_account.id,
    CONTAINER_ID            = var.CONTAINER_ID,
    BUCKET_NAME             = yandex_storage_bucket.bucket.bucket
  })
}

output "api_url" {
  value = yandex_api_gateway.gateway.domain
}

locals {
  roles = toset([
    "container-registry.images.puller",
    "serverless.containers.invoker",
    "storage.admin"
  ])
}

resource "yandex_iam_service_account" "service_account" {
  name = "sa-app"
}

resource "yandex_resourcemanager_folder_iam_member" "roles" {
  for_each  = local.roles
  folder_id = yandex_iam_service_account.service_account.folder_id
  role      = each.key
  member    = "serviceAccount:${yandex_iam_service_account.service_account.id}"
}

resource "yandex_iam_service_account_static_access_key" "static_access_key" {
  service_account_id = yandex_iam_service_account.service_account.id
}

resource "yandex_iam_service_account" "ydb_service_account" {
  name = "sa-app-ydb"
}

resource "yandex_resourcemanager_folder_iam_member" "ydb_role" {
  folder_id = yandex_iam_service_account.service_account.folder_id
  role      = "yds.editor"
  member    = "serviceAccount:${yandex_iam_service_account.ydb_service_account.id}"
}

resource "yandex_iam_service_account_key" "ydb_service_account_key" {
  service_account_id = yandex_iam_service_account.ydb_service_account.id
}

output "service_account_id" {
  value = yandex_iam_service_account.service_account.id
}

output "bucket_access_key" {
  value = yandex_iam_service_account_static_access_key.static_access_key.access_key
}

output "bucket_secret_key" {
  value = yandex_iam_service_account_static_access_key.static_access_key.secret_key
  sensitive = true
}

output "ydb_service_account_key" {
  value = jsonencode({
    "id"=yandex_iam_service_account_key.ydb_service_account_key.id,
    "service_account_id"=yandex_iam_service_account_key.ydb_service_account_key.service_account_id,
    "public_key"=yandex_iam_service_account_key.ydb_service_account_key.public_key,
    "private_key"=yandex_iam_service_account_key.ydb_service_account_key.private_key
  })
  sensitive = true
}

resource "yandex_container_registry" "registry" {
  name = "tewboat-registry"
}

output "container_registry_id" {
  value = yandex_container_registry.registry.id
}

resource "yandex_storage_bucket" "bucket" {
  bucket     = format("example-bucket-%s", yandex_iam_service_account.service_account.id)
  access_key = yandex_iam_service_account_static_access_key.static_access_key.access_key
  secret_key = yandex_iam_service_account_static_access_key.static_access_key.secret_key
}

output "bucket_name" {
  value = yandex_storage_bucket.bucket.bucket
}

resource "yandex_ydb_database_serverless" "app-ydb" {
  name = "wall-ydb-serverless"
}

output "ydb_endpoint" {
  value = yandex_ydb_database_serverless.app-ydb.ydb_api_endpoint
}

output "ydb_database" {
  value = yandex_ydb_database_serverless.app-ydb.database_path
}