"""Read Total Fog's preserved drawing data and check hidden ownership exactly."""
import hashlib
import struct


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


class Reader:
    def __init__(self, data):
        self.data, self.offset = data, 0

    def take(self, count):
        require(0 <= count <= len(self.data) - self.offset, "Truncated observed archive")
        result = self.data[self.offset:self.offset + count]
        self.offset += count
        return result

    def integer(self):
        return struct.unpack("<i", self.take(4))[0]

    def count(self):
        count = self.integer()
        require(0 <= count <= (len(self.data) - self.offset) // 4, "Invalid observed archive count")
        return count

    def records(self):
        return [self.take(self.integer()) for _ in range(self.count())]


def read_archive(path, proof):
    data = path.read_bytes()
    require(len(data) == proof["bytes"] and hashlib.sha256(data).hexdigest().upper() == proof["fingerprint"],
            "Observed archive does not match the scenario")
    reader = Reader(data)
    header = tuple(reader.integer() for _ in range(4))
    require(header[:2] == (0x54464753, 2), "Unknown observed section archive")
    resources = Reader(reader.take(reader.integer()))
    graph = tuple(resources.integer() for _ in range(4))
    require(graph == (0x54464750, 1, 1, 1), "Unknown sparse observation graph")
    tables = {name: resources.records() for name in ("textures", "materials", "meshes")}
    pictures = [[(resources.integer(), resources.integer()) for _ in range(resources.count())]
                for _ in range(resources.count())]
    require(resources.integer() == -1 and resources.offset == len(resources.data), "Invalid observation graph tail")
    records = []
    for _ in range(reader.count()):
        owner = tuple(reader.integer() for _ in range(7))
        cells = tuple(reader.integer() for _ in range(reader.count()))
        require(cells and len(cells) == len(set(cells)) and
                all(0 <= cell < header[2] * header[3] for cell in cells), "Invalid observed owner cells")
        records.append((owner, cells))
    require(reader.offset == len(reader.data), "Trailing observed archive bytes")
    return dict(header=header, pictures=pictures, records=records, **tables)


def compare_hidden_observations(reference_path, reference, loaded_path, loaded):
    before, after = read_archive(reference_path, reference), read_archive(loaded_path, loaded)
    for key in ("header", "pictures", "records"):
        require(before[key] == after[key], "Observed layout or cell ownership changed: " + key)
    visible = set(reference["currentlyVisible"])
    require(visible == set(loaded["currentlyVisible"]), "Comparison visibility changed")
    owned = {cell for _, cells in before["records"] for cell in cells}
    require(visible <= owned and owned - visible, "The archive has no hidden owned cells")
    protected = {name: set() for name in ("meshes", "materials")}
    hidden_owners = 0
    for owner, cells in before["records"]:
        picture_id = owner[6]
        require(0 <= picture_id < len(before["pictures"]), "Invalid observation picture ID")
        for mesh_id, material_id in before["pictures"][picture_id]:
            require(0 <= mesh_id < len(before["meshes"]) and 0 <= material_id < len(before["materials"]),
                    "Invalid observation resource ID")
        if set(cells) - visible:
            hidden_owners += 1
            for mesh_id, material_id in before["pictures"][picture_id]:
                protected["meshes"].add(mesh_id)
                protected["materials"].add(material_id)
    metrics = {"ownedCells": len(owned), "visibleCells": len(visible), "hiddenCells": len(owned - visible),
               "ownersWithHiddenCells": hidden_owners, "layoutAndOwnershipExact": True}
    for name in ("textures", "meshes", "materials"):
        require(len(before[name]) == len(after[name]), "Observed resource count changed: " + name)
        changed = {i for i, (left, right) in enumerate(zip(before[name], after[name])) if left != right}
        require(not changed if name == "textures" else not changed & protected[name],
                "Hidden observed resource changed: " + name)
        metrics[name] = {"count": len(before[name]), "changedVisibleResources": len(changed),
                         "protectedResources": len(before[name]) if name == "textures" else len(protected[name])}
    return metrics
