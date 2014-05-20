using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Disposables;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Wings
{
    class FacadeComponent<TResult> : AsyncActionComponent<TResult>
    {
        readonly List<Tuple<IStateMachine<IComponentState>, MemberInfo>> components = new List<Tuple<IStateMachine<IComponentState>, MemberInfo>>();

        public FacadeComponent()
        {
            var type = typeof(TResult);
            if (!(type.IsPublic || type.IsNestedPublic))
                throw new InvalidOperationException("Type parameter must be public.");
        }

        public void Add<TComponent>(IComponent<TComponent> component, Expression<Func<TResult, TComponent>> member)
        {
            if (component == null)
                throw new ArgumentException("component");

            if (member == null)
                throw new ArgumentNullException("property");

            var body = member.Body;
            MemberInfo memberInfo;
            if (body.NodeType == ExpressionType.Convert)
            {
                var obj = ((MethodCallExpression)((UnaryExpression)body).Operand).Object;
                memberInfo = (MemberInfo)((ConstantExpression)obj).Value;
            }
            else
            {
                memberInfo = ((MemberExpression)body).Member;
            }

            if (!this.Send(ComponentMessages.Freeze))
                throw new InvalidOperationException("Invalid state.");

            try
            {
                components.Add(Tuple.Create((IStateMachine<IComponentState>)component, memberInfo));
            }
            finally
            {
                this.Send(ComponentMessages.Unfreeze);
            }
        }

        protected override async Task StartCore(Run<TResult> run)
        {
            using (new CompositeDisposable(await Task.WhenAll(this.components.Select(cmp => cmp.Item1.StartAndLock()))))
            {
                var dict = this.components.ToDictionary(cmp => cmp.Item2.Name, cmp => ((IHasResult)(cmp.Item1.State)).Result);
                await run(dict.As<TResult>());
            }
        }
    }
}
