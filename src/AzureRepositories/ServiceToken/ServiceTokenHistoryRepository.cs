﻿using System;
using System.Threading.Tasks;
using AzureRepositories.Extensions;
using AzureStorage;
using Core.ServiceToken;

namespace AzureRepositories.ServiceToken
{
    public class ServiceTokenHistoryRepository : IServiceTokenHistoryRepository
    {
        private readonly INoSQLTableStorage<ServiceTokenHistoryEntity> _tableStorage;

        public ServiceTokenHistoryRepository(INoSQLTableStorage<ServiceTokenHistoryEntity> tableStorage)
        {
            _tableStorage = tableStorage;
        }

        public async Task SaveTokenHistoryAsync(IServiceTokenEntity token, string userName, string userIpAddress)
        {
            var th = new ServiceTokenHistoryEntity
            {
                RowKey = DateTime.UtcNow.StorageString(),
                UserName = userName,
                KeyOne = token.SecurityKeyOne,
                KeyTwo = token.SecurityKeyTwo,
                TokenId = token.RowKey,
                UserIpAddress = userIpAddress
            };

            await _tableStorage.InsertOrMergeAsync(th);
        }
    }
}
