﻿// ******************************************************************
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THE CODE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
// TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
// THE CODE OR THE USE OR OTHER DEALINGS IN THE CODE.
// ******************************************************************

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Helpers;
using Newtonsoft.Json;
using System.Threading;

namespace Microsoft.Toolkit.Uwp.SampleApp
{
    public static class Samples
    {
        private const string _recentSamplesStorageKey = "test3";
        //private const string _recentSamplesStorageKey = "uct-recent-samples";

        private static List<SampleCategory> _samplesCategories;
        private static SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        private static LinkedList<Sample> _recentSamples;
        private static RoamingObjectStorageHelper _roamingObjectStorageHelper = new RoamingObjectStorageHelper();

        public static async Task<SampleCategory> GetCategoryBySample(Sample sample)
        {
            return (await GetCategoriesAsync()).FirstOrDefault(c => c.Samples.Contains(sample));
        }

        public static async Task<SampleCategory> GetCategoryByName(string name)
        {
            return (await GetCategoriesAsync()).FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public static async Task<Sample> GetSampleByName(string name)
        {
            return (await GetCategoriesAsync()).SelectMany(c => c.Samples).FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public static async Task<Sample[]> FindSamplesByName(string name)
        {
            var query = name.ToLower();
            return (await GetCategoriesAsync()).SelectMany(c => c.Samples).Where(s => s.Name.ToLower().Contains(query)).ToArray();
        }

        public static async Task<List<SampleCategory>> GetCategoriesAsync()
        {
            await _semaphore.WaitAsync();
            if (_samplesCategories == null)
            {
                List<SampleCategory> allCategories;
                using (var jsonStream = await StreamHelper.GetPackagedFileStreamAsync("SamplePages/samples.json"))
                {
                    var jsonString = await jsonStream.ReadTextAsync();
                    allCategories = JsonConvert.DeserializeObject<List<SampleCategory>>(jsonString);
                }

                // Check API
                var supportedCategories = new List<SampleCategory>();
                foreach (var category in allCategories)
                {
                    var finalSamples = new List<Sample>();

                    foreach (var sample in category.Samples)
                    {
                        if (sample.IsSupported)
                        {
                            finalSamples.Add(sample);
                            await sample.PreparePropertyDescriptorAsync();
                        }
                    }

                    if (finalSamples.Count > 0)
                    {
                        supportedCategories.Add(category);
                        category.Samples = finalSamples.OrderBy(s => s.Name).ToArray();
                    }
                }

                _samplesCategories = supportedCategories.ToList();
            }
            _semaphore.Release();
            return _samplesCategories;
        }

        public static async Task<LinkedList<Sample>> GetRecentSamples()
        {
            if (_recentSamples == null)
            {
                _recentSamples = new LinkedList<Sample>();
                var savedSamples = _roamingObjectStorageHelper.Read<string>(_recentSamplesStorageKey);

                if (savedSamples != null)
                {
                    var sampleNames = savedSamples.Split(';');
                    foreach (var name in sampleNames)
                    {
                        var sample = await GetSampleByName(name);
                        if (sample != null)
                        {
                            _recentSamples.AddFirst(sample);
                        }
                    }
                }
            }

            return _recentSamples;
        }

        public static async Task PushRecentSample(Sample sample)
        {
            var samples = await GetRecentSamples();

            var duplicates = samples.Where(s => s.Name == sample.Name).ToList();
            foreach (var duplicate in duplicates)
            {
                samples.Remove(duplicate);
            }

            samples.AddFirst(sample);
            SaveRecentSamples();
        }

        private static void SaveRecentSamples()
        {
            if (_recentSamples == null)
            {
                return;
            }

            var str = string.Join(";", _recentSamples.Take(10).Select(s => s.Name).ToArray());
            _roamingObjectStorageHelper.Save<string>(_recentSamplesStorageKey, str);
        }
    }
}
