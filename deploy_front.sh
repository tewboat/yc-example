BUCKET_NAME=$(terraform output -raw bucket_name)
BUCKET_ACCESS_KEY=$(terraform output -raw bucket_access_key)
BUCKET_SECRET_KEY=$(terraform output -raw bucket_secret_key)
export API_URL="https://$(terraform output -raw api_url)"

CURRENT_FRONTEND_VERSION="0.0.1"
tmpfile="./Front/build/index.html"
echo $(sed "s/{frontend_version}/$CURRENT_FRONTEND_VERSION/" <<< cat ./Front/index.html) >> $tmpfile

s3cmd --access_key=${BUCKET_ACCESS_KEY} --secret_key=${BUCKET_SECRET_KEY} --host="storage.yandexcloud.net" --host-bucket="%(bucket)s.storage.yandexcloud.net" sync ./Front/build/ s3://${BUCKET_NAME}/
