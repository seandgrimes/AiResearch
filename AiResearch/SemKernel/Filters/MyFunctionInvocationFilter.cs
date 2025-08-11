using Microsoft.SemanticKernel;

namespace SemKernel.Filters;

public class MyFunctionInvocationFilter : IFunctionInvocationFilter
{
  public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
  {
    var arguments = context.Arguments;
    await next(context);
  }
}