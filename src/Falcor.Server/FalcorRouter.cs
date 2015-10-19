using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Falcor.Server.Routing;

namespace Falcor.Server
{
    public class FalcorModel
    {
        private List<FalcorPath> _pathValues = new List<FalcorPath>();


    }


    public abstract class FalcorRouter
    {
        public FalcorRouter()
        {
            //_route = Routes.FirstToComplete();
        }

        public List<Route> Routes { get; } = new List<Route>();
        private readonly FalcorResponseBuilder _responseBuilder = new FalcorResponseBuilder();
        private Lazy<Route> LazyRootRoute => new Lazy<Route>(() => Routes.First());
        private Route RootRoute => LazyRootRoute.Value;

        protected RouteBuilder Get => new RouteBuilder(FalcorMethod.Get, this);
        protected RouteBuilder Set => new RouteBuilder(FalcorMethod.Set, this);
        protected RouteBuilder Call => new RouteBuilder(FalcorMethod.Call, this);

        public static RouteHandlerResult Complete(PathValue value) => Complete(new List<PathValue>(1) { value });
        public static RouteHandlerResult Complete(List<PathValue> values) => new CompleteHandlerResult(values);
        public static RouteHandlerResult Error(string error = null) => new ErrorHandlerResult(error);

        private IObservable<PathValue> Resolve(Route route, RequestContext context)
        {
            if (!context.Unmatched.Any() || _responseBuilder.Contains(context.Unmatched))
                return Observable.Empty<PathValue>();

            var results = route(context).SelectMany(result =>
            {
                if (result.IsComplete)
                {
                    var pathValues = result.Values;
                    if (pathValues.Any())
                    {
                        _responseBuilder.AddRange(pathValues);
                        if (result.UnmatchedPath.Any())
                        {
                            return pathValues.ToObservable()
                                .Where(pathValue => pathValue.Value is Ref)
                                .SelectMany(pathValue =>
                                {
                                    var unmatched = ((Ref)pathValue.Value).AsRef().AppendAll(result.UnmatchedPath);
                                    return Resolve(route, context.WithUnmatched(unmatched));
                                })
                                //.StartWith(pathValues) // Is this nescessary?
                                ;
                        }
                    }
                }
                else
                {
                    var error = new Error(result.Error);
                    _responseBuilder.Add(context.Unmatched, error);
                    return Observable.Return(new PathValue(context.Unmatched, error));
                }
                return Observable.Empty<PathValue>();
            });

            return results;
        }

        public IObservable<PathValue> Route(FalcorRequest request) =>
            request.Paths.ToObservable().SelectMany(unmatched => Resolve(RootRoute, new RequestContext(request, unmatched)));

        public async Task<FalcorResponse> RouteAsync(FalcorRequest request)
        {
            IList<PathValue> result = await Route(request).ToList().ToTask();
            var response = _responseBuilder.CreateResponse();
            return response;
        }
    }
}