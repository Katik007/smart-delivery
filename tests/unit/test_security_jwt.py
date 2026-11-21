import pytest
from src.common.security import hash_password, verify_password, create_access_token, decode_token


def test_password_hashing():
    pwd = "SecretPassword123!"
    hashed = hash_password(pwd)
    assert hashed != pwd
    assert verify_password(pwd, hashed) is True
    assert verify_password("WrongPassword", hashed) is False


def test_jwt_token_creation_and_decoding():
    user_id = "user-abc-123"
    username = "kabanova"
    role = "Manager"

    token = create_access_token(user_id, username, role)
    assert token is not None

    decoded = decode_token(token)
    assert decoded["sub"] == user_id
    assert decoded["username"] == username
    assert decoded["role"] == role
