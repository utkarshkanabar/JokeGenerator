﻿using JokeGenerator.Interfaces;
using JokeGenerator.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.Caching;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace JokeGenerator.Services
{
    /// <summary>
    /// Service implementation for Joke Service Provider 
    /// </summary>
    public class JokeService : IJokeService
    {
        #region fields
        private readonly HttpClient httpClient;
        private readonly ILogger<JokeService> logger;
        private readonly AppSettings appSettings;
        private readonly MemoryCache cache;
        #endregion


        #region ctor
        public JokeService(HttpClient httpClient, ILogger<JokeService> logger,
                        AppSettings appSettings)
        {
            this.httpClient = httpClient;
            this.logger = logger;
            this.appSettings = appSettings;
            this.cache = new MemoryCache("categoryCache");
        }
        #endregion

        #region Methods
        /// <summary>
        /// Joke service api calls to get categories
        /// </summary>
        /// <returns>Retruns key value pair for categories from joke service provider.</returns>
        public async Task<IDictionary<int, string>> GetCategories()
        {
            if (cache.Contains("categories"))
            {
                return GetCachedData<IDictionary<int, string>>("categories");
            }
            else
            {
                var categoriesList = new Dictionary<int, string>();
                try
                {
                    var streamTask = httpClient.GetStreamAsync(appSettings.jokeServiceData.Path);
                    var categories = await JsonSerializer.DeserializeAsync<string[]>(await streamTask);
                    int i = 1;
                    foreach (string category in categories)
                    {
                        categoriesList.Add(i, category);
                        i++;
                    }
                    var cacheItemPolicy = new CacheItemPolicy
                    {
                        AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(appSettings.cachePolicy.AbsouluteExpiry)
                    };
                    cache.Add(new CacheItem("categories", categoriesList), cacheItemPolicy);
                }
                catch (Exception ex)
                {
                    //log and eat the exception and return empty name
                    logger.LogError(string.Format("Exception Occurred While Getting Categories: Message={0} ", ex.Message));
                }

                return categoriesList;
            }
        }

        /// <summary>
        /// Returns multiple jokes by category from joke service provider
        /// </summary>
        /// <param name="category">joke category</param>
        /// <param name="n">number of jokes</param>
        /// <param name="firstName">first name</param>
        /// <param name="lastName">lastname</param>
        /// <returns>Retruns multiple joke by category and also replaces chuck norris with specified first and last name.</returns>
        public async Task<List<string>> GetMultipleJokes(string category, int n, string firstName, string lastName)
        {
            var tasks = new List<Task<string>>();
            for (int i = 0; i < n; i++)
            {
                tasks.Add(GetJoke(category));
            }
            var result = (await Task.WhenAll(tasks)).ToList();
            if (!string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName))
            {
                var updatedResult = new List<string>();
                foreach (string joke in result)
                {
                    updatedResult.Add(joke.Replace("Chuck", firstName).Replace("Norris", lastName));
                }
                return updatedResult;
            }
            return result;

        }

        /// <summary>
        /// Get joke by category from joke service provider
        /// </summary>
        /// <param name="category">joke category</param>
        /// <returns>Retruns single joke by category.</returns>
        public async Task<string> GetJoke(string category)
        {
            var joke = string.Empty;
            UriBuilder builder = new UriBuilder(string.Concat(appSettings.jokeServiceData.BaseAddress, appSettings.jokeServiceData.JokePath));
            if (!string.IsNullOrWhiteSpace(category))
            {
                builder.Query = string.Concat("category=", category);
            }
            try
            {
                var streamTask = httpClient.GetStreamAsync(builder.Uri);
                var jokeData = await JsonSerializer.DeserializeAsync<JokesData>(await streamTask);
                joke = jokeData.Value;
            }
            catch (Exception ex)
            {
                //log and eat the exception and return empty name
                logger.LogError(string.Format("Exception Occurred While Getting Joke: Message={0} ", ex.Message));
            }
            return joke;
        }

        private T GetCachedData<T>(string key)
        {
            try
            {
                if (cache.Contains(key))
                    return (T)cache[key];
                return default(T);
            }
            catch (InvalidCastException ex)
            {
                return default(T);
            }
        }
        #endregion
    }
}
