using System.ComponentModel.DataAnnotations;
using FifthBox.ServerManager.Client.Web.Pages.Workloads.Components;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Client.Web.Tests;

[TestClass]
public class WorkloadFormModelTests
{
    private static List<ValidationResult> Validate(WorkloadFormModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    private static bool Complains(WorkloadFormModel model, string member)
        => Validate(model).Any(r => r.MemberNames.Contains(member));

    [TestMethod]
    public void A_container_needs_an_image_and_says_so_on_the_image_field()
    {
        var model = new WorkloadFormModel { Name = "web", Target = WorkloadTarget.Swarm };

        Assert.IsTrue(Complains(model, nameof(WorkloadFormModel.Image)));

        model.Image = "nginx:1.27";
        Assert.IsFalse(Complains(model, nameof(WorkloadFormModel.Image)));
    }

    [TestMethod]
    public void A_container_is_not_asked_for_an_agent_or_a_command()
    {
        var model = new WorkloadFormModel { Name = "web", Target = WorkloadTarget.Swarm, Image = "nginx:1.27" };

        Assert.IsFalse(Complains(model, nameof(WorkloadFormModel.AgentId)));
        Assert.IsFalse(Complains(model, nameof(WorkloadFormModel.Command)));
    }

    [TestMethod]
    public void A_native_workload_needs_an_agent_and_a_command_but_no_image()
    {
        var model = new WorkloadFormModel { Name = "srv", Target = WorkloadTarget.Agent };

        Assert.IsTrue(Complains(model, nameof(WorkloadFormModel.AgentId)));
        Assert.IsTrue(Complains(model, nameof(WorkloadFormModel.Command)));
        Assert.IsFalse(Complains(model, nameof(WorkloadFormModel.Image)));

        model.AgentId = "a1";
        model.Command = "./server";
        Assert.IsEmpty(Validate(model));
    }

    [TestMethod]
    public void Switching_target_moves_what_is_required()
    {
        // lives in Validate because validity depends on a field the attributes can't see
        var model = new WorkloadFormModel { Name = "x", Target = WorkloadTarget.Agent, AgentId = "a1", Command = "./run" };
        Assert.IsEmpty(Validate(model));

        model.Target = WorkloadTarget.Swarm;
        Assert.IsTrue(Complains(model, nameof(WorkloadFormModel.Image)));
    }

    [TestMethod]
    public void Name_is_required_whatever_it_runs_on()
    {
        Assert.IsTrue(Complains(new WorkloadFormModel { Target = WorkloadTarget.Swarm, Image = "nginx" },
            nameof(WorkloadFormModel.Name)));
    }

    [TestMethod]
    public void Fingerprint_changes_with_any_edit_so_the_dirty_guard_notices()
    {
        var model = new WorkloadFormModel { Name = "web", Image = "nginx:1.27" };
        var baseline = model.Fingerprint();

        Assert.AreEqual(baseline, model.Fingerprint());

        model.Env.Add(new Client.Web.Components.KeyValueItem { Key = "LOG", Value = "debug" });
        Assert.AreNotEqual(baseline, model.Fingerprint());
    }

    [TestMethod]
    public void A_web_app_must_give_a_real_port_instead_of_silently_getting_no_address()
    {
        var model = new WorkloadFormModel { Name = "web", Image = "nginx:1.27", WebApp = true, HttpPort = "eighty" };

        Assert.IsTrue(Complains(model, nameof(WorkloadFormModel.HttpPort)));

        model.HttpPort = "0";
        Assert.IsTrue(Complains(model, nameof(WorkloadFormModel.HttpPort)));

        model.HttpPort = "8080";
        Assert.IsFalse(Complains(model, nameof(WorkloadFormModel.HttpPort)));
    }

    [TestMethod]
    public void A_port_that_is_not_serving_the_web_is_not_asked_for()
    {
        var model = new WorkloadFormModel { Name = "web", Image = "nginx:1.27", WebApp = false, HttpPort = "nonsense" };

        Assert.IsFalse(Complains(model, nameof(WorkloadFormModel.HttpPort)));
    }

    [TestMethod]
    public void A_published_port_row_must_be_a_real_port()
    {
        var model = new WorkloadFormModel
        {
            Name = "game",
            Image = "srv:1",
            Ports = [new PortRow { Published = 0, Target = 25565 }],
        };

        Assert.IsTrue(Complains(model, nameof(WorkloadFormModel.Ports)));

        model.Ports[0].Published = 25565;
        Assert.IsEmpty(Validate(model));
    }

    [TestMethod]
    public void Publishing_ports_asks_for_no_node_at_all()
    {
        // swarm won't stack two tasks on one host port, so it spreads them itself
        var model = new WorkloadFormModel
        {
            Name = "game",
            Image = "srv:1",
            Replicas = 3,
            Ports = [new PortRow { Published = 25565, Target = 25565 }],
        };

        Assert.IsEmpty(Validate(model));
    }

    [TestMethod]
    public void Choosing_the_node_yourself_means_naming_one()
    {
        var model = new WorkloadFormModel
        {
            Name = "gpu", Image = "cuda:1", Placement = WorkloadPlacement.Node,
        };

        Assert.IsTrue(Complains(model, nameof(WorkloadFormModel.NodeId)));

        model.NodeId = "n1";
        Assert.IsEmpty(Validate(model));
    }

    [TestMethod]
    public void Lowering_a_limit_brings_its_reservation_down_with_it()
    {
        // the server rejects reserve over limit, so the form shouldn't let you ask
        var model = new WorkloadFormModel { Name = "db", Image = "postgres:17", MemoryLimitMb = 4096, MemoryReserveMb = 2048 };

        model.SetMemoryLimit(1024);
        Assert.AreEqual(1024, model.MemoryReserveMb);

        model.SetCpuLimit(0);
        Assert.AreEqual(0, model.CpuReserve, "no limit constrains nothing");
    }

    [TestMethod]
    public void Raising_a_limit_leaves_the_reservation_alone()
    {
        var model = new WorkloadFormModel { Name = "db", Image = "postgres:17", MemoryLimitMb = 1024, MemoryReserveMb = 512 };

        model.SetMemoryLimit(8192);
        Assert.AreEqual(512, model.MemoryReserveMb);
    }

    [TestMethod]
    public void No_limit_is_sent_as_null_rather_than_zero()
    {
        var request = new WorkloadFormModel { Name = "web", Image = "nginx" }.ToCreateRequest();

        Assert.IsNull(request.MemoryLimitMb);
        Assert.IsNull(request.CpuLimit);
        Assert.IsNull(request.MemoryReserveMb);
        Assert.IsNull(request.CpuReserve);
    }
}
