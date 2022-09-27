// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Web.ModelBinding;

using System;
using System.ComponentModel;

public sealed class ModelValidatingEventArgs : CancelEventArgs
{

    public ModelValidatingEventArgs(ModelBindingExecutionContext modelBindingExecutionContext, ModelValidationNode parentNode)
    {
        if (modelBindingExecutionContext == null)
        {
            throw new ArgumentNullException(nameof(modelBindingExecutionContext));
        }

        ModelBindingExecutionContext = modelBindingExecutionContext;
        ParentNode = parentNode;
    }

    public ModelBindingExecutionContext ModelBindingExecutionContext
    {
        get;
        private set;
    }

    public ModelValidationNode ParentNode
    {
        get;
        private set;
    }

}