#!/bin/bash

echo -n "Which namespace?: "
read namespace
echo -n "Docker user?: "
read user
echo -n "Docker password?: "
read password
echo -n "Docker email?: "
read email

kubectl create secret docker-registry regcred \
    --namespace=$namespace \
    --docker-username=$user \
    --docker-password=$password \
    --docker-email=$email
