﻿using Core.ServiceToken;
using Microsoft.WindowsAzure.Storage.Table;

namespace AzureRepositories.ServiceToken
{
    public class ServiceTokenEntity : TableEntity, IServiceTokenEntity
    {
        public static string GeneratePartitionKey() => "S";

        public string SecurityKeyOne { get; set; }
        public string SecurityKeyTwo { get; set; }
    }
}
