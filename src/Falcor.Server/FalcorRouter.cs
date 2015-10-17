using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;

namespace Falcor.Server
{
    public abstract class FalcorRouter<TRequest>
    {
        public FalcorRouter()
        {
            _route = Server.Route.FirstOf(Routes);
        }

        private readonly FalcorModel _model = new FalcorModel();

        public List<Route<TRequest>> Routes { get; } = new List<Route<TRequest>>();
        private Route<TRequest> _route { get; }

        protected RouteBuilder<TRequest> Get => new RouteBuilder<TRequest>(FalcorMethod.Get, this);

        private IObservable<PathValue> Resolve(Route<TRequest> route, RequestContext<TRequest> context)
        {
            if (!context.Unmatched.Any() || _model.Contains(context.Unmatched))
                return Observable.Empty<PathValue>();

            var results = route(context).SelectMany(result =>
            {
                if (result.IsComplete)
                {
                    var pathValues = result.Values;
                    if (pathValues.Any())
                    {
                        //TODO: this.model.putAll(pathValues);
                        if (result.UnmatchedPath.Any())
                        {
                            return pathValues.ToObservable()
                                .Where(pathValue => pathValue.Value.IsRef)
                                .SelectMany(pathValue =>
                                {
                                    var unmatched = pathValue.Value.AsRef().AppendAll(result.UnmatchedPath);
                                    return Resolve(route, context.WithPaths(FalcorPath.Empty, unmatched));
                                })
                                //.StartWith(pathValues) // Is this nescessary?
                                ;
                        }
                    }
                }
                else
                {
                    var error = new Error(result.Error);
                    //TODO: this.model.put(context.getUnmatched(), (FalcorValue)error);
                    return Observable.Return(new PathValue(context.Unmatched, error));
                }
                return Observable.Empty<PathValue>();
            });

            return results;
        }

        public IObservable<PathValue> Route(FalcorRequest<TRequest> request) =>
            request.Paths.ToObservable().SelectMany(
                path => Resolve(_route, new RequestContext<TRequest>(request, FalcorPath.Empty, path)));
    }
}