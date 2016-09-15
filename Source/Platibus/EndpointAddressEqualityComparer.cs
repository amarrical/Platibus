﻿// The MIT License (MIT)
// 
// Copyright (c) 2016 Jesse Sweetland
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;

namespace Platibus
{
    /// <summary>
    /// Determines the equality of two endpoints based on the left part of their respective address URIs
    /// </summary>
    public class EndpointAddressEqualityComparer : IEqualityComparer<Uri>
    {
        /// <summary>
        /// Determines whether the specified endpoint address URIs are equal.
        /// </summary>
        /// <returns>
        /// true if the specified URIs are equal; otherwise, false.
        /// </returns>
        /// <param name="x">The first endpoint address URI to compare.</param>
        /// <param name="y">The second endpoint address URI to compare.</param>
        public bool Equals(Uri x, Uri y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(null, x)) return false;
            if (ReferenceEquals(null, y)) return false;
            return string.Equals(
                x.GetLeftPart(UriPartial.Path).TrimEnd('/'),
                y.GetLeftPart(UriPartial.Path).TrimEnd('/'),
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns a hash code for the specified object.
        /// </summary>
        /// <returns>
        /// A hash code for the specified object.
        /// </returns>
        /// <param name="obj">The <see cref="T:System.Object"/> for which a hash code is to be returned.</param>
        /// <exception cref="T:System.ArgumentNullException">The type of <paramref name="obj"/> is a reference type and <paramref name="obj"/> is null.</exception>
        public int GetHashCode(Uri obj)
        {
            return obj == null ? 0 : obj
                .GetLeftPart(UriPartial.Path).TrimEnd('/')
                .ToLower()
                .GetHashCode();
        }
    }
}
